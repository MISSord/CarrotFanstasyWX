using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor 侧 AB 构建与清单生成。
///
/// BuildAssetBundles：调用 Unity BuildPipeline，输出到平台目录。
/// GenerateManifest：以「磁盘上实际存在的 AB 文件」为准写 custom_manifest.json，
/// 再调用 PackMerger 生成 packs/*.zip 与 DownloadPacks。
///
/// 清单字段与运行时校验的对应关系：
/// - Size / Hash：构建产物的字节大小与 MD5（下载后二次校验用）
/// - CompressedFormat：0 时运行时会再压成 LZ4，启动校验不能再用该 Hash 对转换后文件
/// - Dependencies：扁平依赖列表，供加载时预拉依赖包
/// </summary>
public static class AssetBundlePackager
{
    public static readonly BuildTarget[] availablePlatforms = {
        BuildTarget.StandaloneWindows,
        BuildTarget.StandaloneWindows64,
        BuildTarget.StandaloneOSX,
        BuildTarget.Android,
        BuildTarget.iOS,
        BuildTarget.WebGL,
    };

    public static readonly string[] platformNames = {
        "Windows 32-bit",
        "Windows 64-bit",
        "macOS",
        "Android",
        "iOS",
        "WebGL",
    };

    /// <summary>
    /// 构建 AB 到指定平台输出目录（如 .../StandaloneWindows）。
    /// clearFolders 为 true 时会整目录删除；仅拒绝盘符根与工程根，输出路径请勿指到桌面等。
    /// </summary>
    public static bool BuildAssetBundles(string outputPath,
                                        BuildTarget buildTarget,
                                        BuildAssetBundleOptions compression = BuildAssetBundleOptions.None,
                                        bool clearFolders = false,
                                        bool copyToStreamingAssets = false)
    {
        try
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("输出路径不能为空！");
                return false;
            }

            if (clearFolders && Directory.Exists(outputPath))
            {
                if (!IsSafeToDeleteDirectory(outputPath))
                {
                    Debug.LogError("输出目录过于危险，已拒绝清空: " + outputPath);
                    return false;
                }
                Directory.Delete(outputPath, true);
                Debug.Log("已清空输出文件夹: " + outputPath);
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                Debug.Log("已创建输出目录: " + outputPath);
            }

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, compression, buildTarget);
            if (manifest == null)
            {
                Debug.LogError("BuildPipeline.BuildAssetBundles 返回空结果。");
                return false;
            }

            Debug.Log($"AssetBundle 已生成。路径: {outputPath} 平台: {buildTarget}");

            if (copyToStreamingAssets)
            {
                CopyToStreamingAssets(outputPath, buildTarget);
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("AssetBundle 打包失败: " + e.Message);
            return false;
        }
    }

    /// <summary>拷贝到 StreamingAssets/AssetBundles/{平台}/，与运行时 GetRuntimeLoadPath 一致。</summary>
    public static void CopyToStreamingAssets(string platformBundlePath, BuildTarget buildTarget)
    {
        string platformFolder = GetPlatformFolder(buildTarget);
        string destPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", platformFolder);

        if (Directory.Exists(destPath))
        {
            if (!IsSafeToDeleteDirectory(destPath))
            {
                Debug.LogError("StreamingAssets 目标目录不安全，已拒绝覆盖: " + destPath);
                return;
            }
            Directory.Delete(destPath, true);
        }

        string parentDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        FileUtil.CopyFileOrDirectory(platformBundlePath, destPath);
        Debug.Log("已复制到 StreamingAssets: " + destPath);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 平台目录名。Win32 与 Win64 均映射为 StandaloneWindows，与运行时 GetRuntimePlatformFolder 一致。
    /// </summary>
    public static string GetPlatformFolder(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "StandaloneWindows";
            case BuildTarget.StandaloneOSX:
                return "StandaloneOSX";
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            case BuildTarget.WebGL:
                return "WebGL";
            default:
                return target.ToString();
        }
    }

    public static BuildAssetBundleOptions GetCompressionOption(CompressionType compression)
    {
        switch (compression)
        {
            case CompressionType.NoCompression:
                return BuildAssetBundleOptions.UncompressedAssetBundle;
            case CompressionType.StandardCompression:
                return BuildAssetBundleOptions.None;
            case CompressionType.ChunkBasedCompression:
                return BuildAssetBundleOptions.ChunkBasedCompression;
            default:
                return BuildAssetBundleOptions.None;
        }
    }

    public static string GetBundlePath(string outputPath, string path)
    {
        string repath = path.Replace('\\', '/');
        repath = repath.Replace(outputPath.Replace('\\', '/') + "/", "");
        return repath.ToLower();
    }

    /// <summary>
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
            EditorUtility.DisplayDialog("错误", "AB 包目录不存在！", "确定");
            return null;
        }

        // HybridCLR 的 .dll.bytes 不是 Unity AB：需在扫描清单前拷入输出目录。
        // 完整打 AB 若勾选清空输出，会删掉此前手动同步的 hybridclr/，必须在此补回。
        CarrotFantasy.Editor.HybridCLRProjectSetup.EnsureDllsInAbOutput(target);

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
        var missingOnDisk = CollectRegisteredButMissingOnDisk(bundlePath);

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

                HashSet<string> processedBundles = new HashSet<string>();
                // HybridCLR 原始 DLL 不是 Unity AB，无依赖图。
                if (!CarrotFantasy.HybridCLRPaths.IsHybridClrRawFile(bundleKey))
                {
                    GenerateFlatDependencyList(registeredName, 0, processedBundles);
                }

                info.Dependencies = processedBundles
                    .Select(NormalizeBundleName)
                    .ToArray();

                generatedManifest.AssetBundles.Add(info);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        LogMissingOnDiskSummary(missingOnDisk);

        // 合并 Pack 必须在写 JSON 之前，以便 DownloadPacks 一并序列化。
        AssetBundlePackMerger.BuildPacks(generatedManifest, bundlePath);

        string manifestJson = JsonUtility.ToJson(generatedManifest, true);
        string manifestPath = Path.Combine(bundlePath, "custom_manifest.json");
        File.WriteAllText(manifestPath, manifestJson);

        string versionPath = Path.Combine(bundlePath, "version.txt");
        File.WriteAllText(versionPath, generatedManifest.BuildTime);

        AssetDatabase.Refresh();
        if (showSuccessDialog)
        {
            EditorUtility.DisplayDialog(
                "成功",
                string.Format(
                    "清单已生成：{0} 个 AB 包（以输出目录实际文件为准）。\n" +
                    "有资源登记但未构建出文件：{1} 个（详见 Console，多为空文件夹包名或需重新打 AB）。",
                    generatedManifest.AssetBundles.Count,
                    missingOnDisk.Count),
                "确定");
        }

        Debug.Log(string.Format(
            "[AB Build] custom_manifest.json 已生成：输出目录 {0} 个包，登记但未落盘 {1} 个。",
            generatedManifest.AssetBundles.Count,
            missingOnDisk.Count));
        return generatedManifest;
    }

    /// <summary>
    /// 已在 AssetDatabase 登记且仍有关联资源，但输出目录找不到对应 AB 文件。
    /// 不含「无资源引用的历史包名」（已由 RemoveUnusedAssetBundleNames 清理）。
    /// </summary>
    private static List<string> CollectRegisteredButMissingOnDisk(string bundlePath)
    {
        var missing = new List<string>();
        string[] registeredNames = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < registeredNames.Length; i++)
        {
            string registeredName = registeredNames[i];
            string bundleKey = NormalizeBundleName(registeredName);
            if (AssetDatabase.GetAssetPathsFromAssetBundle(registeredName).Length == 0)
            {
                continue;
            }

            if (File.Exists(ResolveBuiltBundleFilePath(bundlePath, bundleKey)))
            {
                continue;
            }

            missing.Add(bundleKey);
        }

        missing.Sort();
        return missing;
    }

    private static void LogMissingOnDiskSummary(List<string> missingOnDisk)
    {
        if (missingOnDisk.Count == 0)
        {
            return;
        }

        const int maxLines = 30;
        string preview = missingOnDisk.Count <= maxLines
            ? string.Join("\n", missingOnDisk)
            : string.Join("\n", missingOnDisk.GetRange(0, maxLines))
              + string.Format("\n... 还有 {0} 个未列出", missingOnDisk.Count - maxLines);

        Debug.LogWarning(string.Format(
            "[AB Build] {0} 个「有资源登记但未构建出文件」的 AB 未写入清单（missingOnDisk 诊断）：\n{1}",
            missingOnDisk.Count,
            preview));
    }

    private static string FindRegisteredBundleName(string bundleKey)
    {
        string[] registeredNames = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < registeredNames.Length; i++)
        {
            if (NormalizeBundleName(registeredNames[i]) == bundleKey)
            {
                return registeredNames[i];
            }
        }

        return bundleKey;
    }

    private static bool IsPlatformManifestBundleFile(string bundleKey, BuildTarget target)
    {
        return bundleKey == GetPlatformFolder(target).ToLowerInvariant();
    }

    private static string NormalizeBundleName(string bundleName)
    {
        return (bundleName ?? string.Empty).ToLower().Replace('\\', '/');
    }

    private static string ResolveBuiltBundleFilePath(string platformOutputPath, string bundleName)
    {
        string normalized = NormalizeBundleName(bundleName);
        string[] parts = normalized.Split('/');
        string fullPath = platformOutputPath;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            fullPath = Path.Combine(fullPath, parts[i]);
        }

        return fullPath;
    }

    private static bool ShouldSkipManifestFile(string file)
    {
        return file.EndsWith(".meta")
            || file.EndsWith(".manifest")
            || file.EndsWith(".json")
            || file.EndsWith(".txt");
    }

    private static void GenerateFlatDependencyList(string abName, int depth, HashSet<string> processedBundles)
    {
        if (processedBundles.Contains(abName))
        {
            return;
        }

        if (depth != 0)
        {
            processedBundles.Add(abName);
        }

        string[] dependencies = AssetDatabase.GetAssetBundleDependencies(abName, true);

        foreach (string dependency in dependencies)
        {
            GenerateFlatDependencyList(dependency, depth + 1, processedBundles);
        }
    }

    private static bool IsSafeToDeleteDirectory(string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootPath = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(rootPath))
        {
            return false;
        }

        if (string.Equals(fullPath, rootPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullPath, projectRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

public enum CompressionType
{
    StandardCompression,
    ChunkBasedCompression,
    NoCompression
}
