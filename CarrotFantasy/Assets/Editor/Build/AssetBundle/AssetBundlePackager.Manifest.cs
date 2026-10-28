using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="AssetBundlePackager"/> 的清单部分（partial class）。
/// <para>
/// 为什么拆文件：主文件负责"构建 + 路径 + 压缩"，清单部分负责
/// "扫描落盘 AB → Size/MD5/依赖 → 写 JSON → 签名 → version.txt"，
/// 两者生命周期不同（清单也能由步骤执行器单独调用），放一起会让主文件越来越难读。
/// </para>
/// <para>
/// 私有工具方法（CollectRegisteredButMissingOnDisk / FindRegisteredBundleName /
/// CollectFlatDependencies 等）仍留在主文件，partial 类内可直接访问。
/// </para>
/// </summary>
public static partial class AssetBundlePackager
{    /// <summary>
    /// 生成 custom_manifest.json。
    ///
    /// 流程：
    /// 1. 扫描平台输出目录中的 AB 文件（跳过 .meta/.manifest/.json/.txt、平台总清单文件、packs/）
    /// 2. 为每个文件写 BundleName / Size / MD5 / 扁平依赖
    /// 3. 诊断「AssetDatabase 有登记但未落盘」的包（只告警，不写入清单）
    /// 4. PackMerger 合并 ZIP，填充 DownloadPacks
    /// 5. 落盘 custom_manifest.json 与 version.txt
    /// </summary>
    public static CustomManifest GenerateManifest(
        string bundleRootPath,
        BuildTarget target,
        int versionNumber = 1,
        int compressedFormat = 0,
        bool showSuccessDialog = true)
    {
        string bundlePath = Path.Combine(bundleRootPath, GetPlatformFolder(target));
        if (!Directory.Exists(bundlePath))
        {
            const string missingMsg = "AB 包目录不存在！";
            Debug.LogError("[AB Build] " + missingMsg + " path=" + bundlePath);
            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog("错误", missingMsg, "确定");
            }

            return null;
        }

        ManifestBuildData data = BuildManifestData(bundlePath, target, versionNumber, compressedFormat);
        if (data == null || data.Manifest == null)
        {
            return null;
        }

        if (data.MissingOnDisk != null && data.MissingOnDisk.Count > 0)
        {
            string missingMsg = data.MissingOnDisk.Count
                + " 个已登记资源没有打出 AB 文件，已中止（详见 Console）。";
            Debug.LogError("[AB Build] " + missingMsg);
            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog("清单未生成", missingMsg, "确定");
            }

            return null;
        }

        // 合并 Pack 必须在写 JSON 之前，以便 DownloadPacks 一并序列化。
        AssetBundlePackMerger.BuildPacks(data.Manifest, bundlePath);

        if (!WriteManifestJson(data.Manifest, bundlePath, out string writeError))
        {
            Debug.LogError("[AB Build] " + writeError);
            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog("写入清单失败", writeError, "确定");
            }

            return null;
        }

        if (!SignManifestFile(bundlePath, out string signError))
        {
            Debug.LogError("[AB Build] " + signError);
            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog("清单签名失败", signError, "确定");
            }

            return null;
        }

        WriteVersionFile(data.Manifest, bundlePath);
        AssetDatabase.Refresh();

        if (showSuccessDialog)
        {
            EditorUtility.DisplayDialog(
                "成功",
                string.Format(
                    "清单已生成：{0} 个 AB 包（以输出目录实际文件为准）。\n" +
                    "有资源登记但未构建出文件：{1} 个（详见 Console，多为空文件夹包名或需重新打 AB）。",
                    data.Manifest.AssetBundles.Count,
                    data.MissingOnDisk.Count),
                "确定");
        }

        Debug.Log(string.Format(
            "[AB Build] custom_manifest.json 已生成：输出目录 {0} 个包，登记但未落盘 {1} 个。",
            data.Manifest.AssetBundles.Count,
            data.MissingOnDisk.Count));
        return data.Manifest;
    }

    /// <summary>
    /// 清单数据 + 诊断信息。
    /// <para>拆步骤后由 GenerateManifestData / MergePacks / WriteManifestJson 等共享。</para>
    /// </summary>
    public sealed class ManifestBuildData
    {
        public CustomManifest Manifest;

        /// <summary>「AssetDatabase 有登记但输出目录没有文件」的包名（只诊断，不进清单）。</summary>
        public List<string> MissingOnDisk;
    }

    /// <summary>
    /// 扫描落盘 AB，构建清单数据（Size / MD5 / 扁平依赖）。
    /// <para>不写文件、不合并 Pack、不签名 —— 这些由调用方或后续步骤负责。</para>
    /// </summary>
    public static ManifestBuildData BuildManifestData(
        string bundlePath,
        BuildTarget target,
        int versionNumber,
        int compressedFormat)
    {
        // 清单 / 运行时都按小写包名找文件，这里把构建产物统一成小写，避免大小写敏感平台找不到。
        NormalizeBundleFileNames(bundlePath, target);

        // 清理 AssetDatabase 里已无资源引用的历史包名，避免 GetAllAssetBundleNames 虚高。
        AssetDatabase.RemoveUnusedAssetBundleNames();

        CustomManifest generatedManifest = new CustomManifest
        {
            AppVersion = Application.version,
            BuildTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            buildTime = System.DateTime.Now.Ticks,
            ManifestVersion = versionNumber,
            // 与 CompressionType 枚举序号一致，运行时用 CompressedFormat==0 判断是否需 LZ4 再压缩。
            CompressedFormat = compressedFormat,
        };

        string[] bundleFiles = Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories);
        List<string> missingOnDisk = CollectRegisteredButMissingOnDisk(bundlePath);

        try
        {
            for (int i = 0; i < bundleFiles.Length; i++)
            {
                string file = bundleFiles[i];
                if (ShouldSkipManifestFile(file))
                {
                    continue;
                }

                string bundleKey = GetBundlePath(bundlePath, file);
                // Unity 会在输出目录生成与平台同名的总清单 AB，不进入热更列表。
                if (IsPlatformManifestBundleFile(bundleKey, target))
                {
                    continue;
                }

                // packs/ 是合并下载产物，由 DownloadPacks 描述，不作为独立 AB 条目。
                if (bundleKey.StartsWith(PackGroupingDefaults.PacksFolderName + "/", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(PackGroupingDefaults.PackFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "生成AB清单",
                    bundleKey,
                    (i + 1f) / bundleFiles.Length);

                string registeredName = FindRegisteredBundleName(bundleKey);
                CustomAssetBundleInfo info = new CustomAssetBundleInfo
                {
                    AssetName = Path.GetFileName(file),
                    BundleName = bundleKey,
                    Size = new FileInfo(file).Length,
                    Hash = MD5Checker.ComputeFileMD5(file),
                };

                // GetAssetBundleDependencies(name, true) 本身已是递归的扁平结果，
                // 不要再对每个依赖递归一次（旧实现是 O(N×D²)，包多时清单阶段明显变慢）。
                info.Dependencies = CollectFlatDependencies(registeredName);

                generatedManifest.AssetBundles.Add(info);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        LogMissingOnDiskSummary(missingOnDisk);

        return new ManifestBuildData
        {
            Manifest = generatedManifest,
            MissingOnDisk = missingOnDisk,
        };
    }

    /// <summary>
    /// 取某个包的直接 + 间接依赖（扁平、去重、排序）。
    /// <para>依赖来源是 AssetDatabase 的包图；SBP 阶段 C 会切换为构建结果的 BundleInfos。</para>
    /// </summary>
    private static string[] CollectFlatDependencies(string bundleName)
    {
        string[] dependencies = AssetDatabase.GetAssetBundleDependencies(bundleName, true);
        if (dependencies == null || dependencies.Length == 0)
        {
            return Array.Empty<string>();
        }

        string self = NormalizeBundleName(bundleName);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string dependency = NormalizeBundleName(dependencies[i]);
            if (string.IsNullOrEmpty(dependency)
                || string.Equals(dependency, self, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            unique.Add(dependency);
        }

        var result = new List<string>(unique);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    /// <summary>写 custom_manifest.json（不做签名）。</summary>
    public static bool WriteManifestJson(CustomManifest manifest, string bundlePath, out string error)
    {
        error = null;
        if (manifest == null)
        {
            error = "清单为空，无法写入";
            return false;
        }

        try
        {
            string manifestJson = JsonUtility.ToJson(manifest, true);
            string manifestPath = Path.Combine(bundlePath, "custom_manifest.json");
            File.WriteAllText(manifestPath, manifestJson);
            AssetBundleBuildSettings.RememberManifestVersion(bundlePath, manifest.ManifestVersion);
            return true;
        }
        catch (System.Exception e)
        {
            error = "写 custom_manifest.json 失败: " + e.Message;
            return false;
        }
    }

    /// <summary>对已写出的 custom_manifest.json 做签名（开发宏生效时只告警）。</summary>
    public static bool SignManifestFile(string bundlePath, out string error)
    {
        string manifestPath = Path.Combine(bundlePath, "custom_manifest.json");
        if (!File.Exists(manifestPath))
        {
            error = "清单文件不存在: " + manifestPath;
            return false;
        }

        return ManifestSigner.TrySign(manifestPath, out error);
    }

    /// <summary>写 version.txt（内容为清单里的 BuildTime）。</summary>
    public static void WriteVersionFile(CustomManifest manifest, string bundlePath)
    {
        string versionPath = Path.Combine(bundlePath, "version.txt");
        File.WriteAllText(versionPath, manifest.BuildTime);
    }

}
