using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Pipeline.Interfaces;
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
    // 运行时用的是 File.Exists + AssetBundle.LoadFromFile，只有 PC/macOS 的同步文件 IO 能这么干。
    // Android/iOS/WebGL 需要先补 UnityWebRequestAssetBundle 分支再放开，否则等于默认出一个跑不起来的包。
    public static readonly BuildTarget[] availablePlatforms = {
        BuildTarget.StandaloneWindows64,
        BuildTarget.StandaloneOSX,
    };

    public static readonly string[] platformNames = {
        "Windows 64-bit",
        "macOS",
    };

    /// <summary>
    /// 是否支持该平台的 AB 构建。
    /// <para>
    /// 32 位 Windows 已明确拒绝：输出目录与 64 位共用 <c>StandaloneWindows</c>，
    /// 交替打包会互相覆盖，且运行时只有 64 位产物。
    /// </para>
    /// </summary>
    public static bool IsSupportedRuntimePlatform(BuildTarget target)
    {
        return target == BuildTarget.StandaloneWindows64
               || target == BuildTarget.StandaloneOSX;
    }

    private const string UseSbpPrefKey = "AB_UseScriptableBuildPipeline";

    /// <summary>
    /// 构建后端：true = Scriptable Build Pipeline（<see cref="AssetBundleSbpPipeline"/>），
    /// false = built-in <c>BuildPipeline.BuildAssetBundles</c>。
    /// <para>
    /// 用 EditorPrefs 持久化，菜单 <c>Tools/AssetBundle/构建后端</c> 可随时切换；
    /// 迁移期间用它做 A/B 对比（同一份资源、同一套清单，只换构建实现）。
    /// </para>
    /// </summary>
    public static bool UseScriptableBuildPipeline
    {
        get { return EditorPrefs.GetBool(UseSbpPrefKey, true); }
        set { EditorPrefs.SetBool(UseSbpPrefKey, value); }
    }

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
        return BuildAssetBundles(
            outputPath, buildTarget, compression, clearFolders, copyToStreamingAssets,
            null, out _);
    }

    /// <summary>
    /// 带「后端覆盖 + SBP 结果输出」的重载。
    /// <para>
    /// backend 为 null 时沿用全局开关（菜单 / EditorPrefs）；步骤执行器传 Profile 的后端。
    /// SBP 构建成功时通过 <paramref name="sbpResults"/> 返回 IBundleBuildResults，
    /// 供清单阶段读取 BundleInfos（Hash / Dependencies），不再依赖 AssetDatabase 的包图。
    /// </para>
    /// </summary>
    public static bool BuildAssetBundles(string outputPath,
                                        BuildTarget buildTarget,
                                        BuildAssetBundleOptions compression,
                                        bool clearFolders,
                                        bool copyToStreamingAssets,
                                        AbBuildBackend? backend,
                                        out IBundleBuildResults sbpResults)
    {
        sbpResults = null;
        bool useSbp = backend.HasValue
            ? backend.Value == AbBuildBackend.ScriptableBuildPipeline
            : UseScriptableBuildPipeline;

        try
        {
            if (!IsSupportedRuntimePlatform(buildTarget))
            {
                Debug.LogError(
                    "当前 AssetBundle 运行时只支持 PC/macOS 的 File IO 加载，" + buildTarget
                    + " 需要先补 UnityWebRequestAssetBundle 分支再打包。");
                return false;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("输出路径不能为空！");
                return false;
            }

            if (clearFolders)
            {
                bool existed = Directory.Exists(outputPath);
                if (!TryCleanOutputDirectory(outputPath, out string cleanError))
                {
                    Debug.LogError(cleanError);
                    return false;
                }

                if (existed)
                {
                    Debug.Log("已清空输出文件夹: " + outputPath);
                }
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                Debug.Log("已创建输出目录: " + outputPath);
            }

            if (useSbp)
            {
                if (!AssetBundleSbpPipeline.Build(
                        outputPath, buildTarget, compression, out string sbpError, out sbpResults))
                {
                    Debug.LogError(
                        "SBP 构建失败: " + sbpError
                        + "\n可临时切回 built-in：菜单 Tools/AssetBundle/构建后端 → 使用 built-in（BuildPipeline）");
                    return false;
                }
            }
            else
            {
                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, compression, buildTarget);
                if (manifest == null)
                {
                    Debug.LogError("BuildPipeline.BuildAssetBundles 返回空结果。");
                    return false;
                }
            }

            Debug.Log($"AssetBundle 已生成。路径: {outputPath} 平台: {buildTarget} 后端: "
                      + (useSbp ? "SBP" : "built-in"));

            if (copyToStreamingAssets && !CopyToStreamingAssets(outputPath, buildTarget))
            {
                return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("AssetBundle 打包失败: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 把构建输出里的 AB 文件名统一成小写（与清单 / 运行时 GetBundleFileName 一致）。
    /// Windows 上只改大小写时 File.Exists 会认为目标已存在，必须先挪到临时名再改回。
    /// </summary>
    public static void NormalizeBundleFileNames(string platformBundlePath, BuildTarget target)
    {
        if (!Directory.Exists(platformBundlePath))
        {
            return;
        }

        string root = Path.GetFullPath(platformBundlePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string platformManifestName = GetPlatformFolder(target).ToLowerInvariant();
        string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        var relatives = new List<string>(files.Length);
        string rootedPrefix = root + Path.DirectorySeparatorChar;
        string rootedAltPrefix = root + Path.AltDirectorySeparatorChar;
        for (int i = 0; i < files.Length; i++)
        {
            string full = files[i];
            string relative;
            if (full.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                relative = full.Substring(rootedPrefix.Length);
            }
            else if (full.StartsWith(rootedAltPrefix, StringComparison.OrdinalIgnoreCase))
            {
                relative = full.Substring(rootedAltPrefix.Length);
            }
            else
            {
                continue;
            }

            relative = relative.Replace('\\', '/');
            if (ShouldSkipBundleFileNameNormalize(relative))
            {
                continue;
            }

            relatives.Add(relative);
        }

        for (int i = 0; i < relatives.Count; i++)
        {
            string relative = relatives[i];
            string lower = relative.ToLowerInvariant();
            if (lower == platformManifestName || string.Equals(relative, lower, StringComparison.Ordinal))
            {
                continue;
            }

            string source = ResolveCaseInsensitivePath(root, relative);
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogWarning("[AB Build] 归一文件名时找不到源文件: " + relative);
                continue;
            }

            int slash = lower.LastIndexOf('/');
            string parentRelative = slash < 0 ? string.Empty : lower.Substring(0, slash);
            string destDirectory = EnsureLowercaseDirectoryChain(root, parentRelative);
            string dest = Path.Combine(destDirectory, Path.GetFileName(lower));

            // 目录改大小写后，原来的绝对路径在区分大小写的盘上会失效，再解析一次。
            source = ResolveCaseInsensitivePath(root, relative);
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogWarning("[AB Build] 归一文件名时找不到源文件: " + relative);
                continue;
            }

            MoveFileFixingCase(source, dest, relative, lower);
        }
    }

    static bool ShouldSkipBundleFileNameNormalize(string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return true;
        }

        if (relative.IndexOf(".__case__", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return relative.StartsWith(PackGroupingDefaults.PacksFolderName + "/", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".sig", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从平台根按路径逐段做大小写不敏感查找，返回磁盘上的实际路径。</summary>
    static string ResolveCaseInsensitivePath(string root, string relative)
    {
        string[] parts = relative.Split('/');
        string current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            bool isLast = i == parts.Length - 1;
            string next = isLast
                ? FindChildFile(current, parts[i])
                : FindChildDirectory(current, parts[i]);
            if (string.IsNullOrEmpty(next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    static string FindChildFile(string directory, string name)
    {
        FileInfo[] files = new DirectoryInfo(directory).GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (string.Equals(files[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return files[i].FullName;
            }
        }

        return null;
    }

    static string FindChildDirectory(string directory, string name)
    {
        DirectoryInfo[] children = new DirectoryInfo(directory).GetDirectories();
        for (int i = 0; i < children.Length; i++)
        {
            if (string.Equals(children[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return children[i].FullName;
            }
        }

        return null;
    }

    /// <summary>把相对目录的每一段改成小写。只改大小写时先经过临时目录名，避免 Windows 认为目标已存在。</summary>
    static string EnsureLowercaseDirectoryChain(string root, string relativeDirLower)
    {
        string current = root;
        if (string.IsNullOrEmpty(relativeDirLower))
        {
            return current;
        }

        string[] parts = relativeDirLower.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            current = EnsureChildDirectoryCase(current, parts[i]);
        }

        return current;
    }

    static string EnsureChildDirectoryCase(string parentFull, string desiredName)
    {
        DirectoryInfo parent = new DirectoryInfo(parentFull);
        if (!parent.Exists)
        {
            string created = Path.Combine(parentFull, desiredName);
            Directory.CreateDirectory(created);
            return created;
        }

        DirectoryInfo[] children = parent.GetDirectories();
        for (int i = 0; i < children.Length; i++)
        {
            if (!string.Equals(children[i].Name, desiredName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(children[i].Name, desiredName, StringComparison.Ordinal))
            {
                return children[i].FullName;
            }

            string tempPath = Path.Combine(parentFull, desiredName + ".__case__" + Guid.NewGuid().ToString("N"));
            string finalPath = Path.Combine(parentFull, desiredName);
            Directory.Move(children[i].FullName, tempPath);
            Directory.Move(tempPath, finalPath);
            return finalPath;
        }

        string path = Path.Combine(parentFull, desiredName);
        Directory.CreateDirectory(path);
        return path;
    }

    static void MoveFileFixingCase(string source, string dest, string relative, string lower)
    {
        string fullSource = Path.GetFullPath(source);
        string fullDest = Path.GetFullPath(dest);
        if (string.Equals(fullSource, fullDest, StringComparison.Ordinal))
        {
            return;
        }

        bool samePathIgnoreCase = string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase);
        if (File.Exists(fullDest) && samePathIgnoreCase)
        {
            string temp = fullDest + ".__case__" + Guid.NewGuid().ToString("N");
            File.Move(fullSource, temp);
            File.Move(temp, fullDest);
            return;
        }

        if (File.Exists(fullDest))
        {
            Debug.LogError("[AB Build] 大小写冲突，已跳过: " + relative + " / " + lower);
            return;
        }

        File.Move(fullSource, fullDest);
    }

    /// <summary>拷贝到 StreamingAssets/AssetBundles/{平台}/，与运行时 GetRuntimeLoadPath 一致。</summary>
    public static bool CopyToStreamingAssets(string platformBundlePath, BuildTarget buildTarget)
    {
        // 保证源目录里的文件名已是小写，拷进 StreamingAssets 后与清单一致。
        NormalizeBundleFileNames(platformBundlePath, buildTarget);

        string platformFolder = GetPlatformFolder(buildTarget);
        string destPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", platformFolder);

        if (Directory.Exists(destPath))
        {
            if (!IsSafeToDeleteDirectory(destPath))
            {
                Debug.LogError("StreamingAssets 目标目录不安全，已拒绝覆盖: " + destPath);
                return false;
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
        return true;
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
    /// 3. 诊断「AssetDatabase 有登记但未落盘」的包（有缺失则中止，不写清单）
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
            string missingMessage = data.MissingOnDisk.Count
                + " 个已登记的包没有打出文件，已中止写清单。详见 Console。";
            Debug.LogError("[AB Build] " + missingMessage);
            if (showSuccessDialog)
            {
                EditorUtility.DisplayDialog("清单不完整", missingMessage, "确定");
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
        int compressedFormat,
        Func<string, string[]> sbpDependencyResolver = null)
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

        // 阶段 A：依赖来源双算（AssetDatabase 为准，SBP 结果只用于对比报告）。
        int dependencyDiffCount = 0;
        int dependencySbpOnlyTotal = 0;
        int dependencyDbOnlyTotal = 0;
        var dependencyDiffSamples = new List<string>();
        const int MaxDependencyDiffSamples = 10;

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

                // 阶段 A：SBP 结果可用时再算一遍。
                // 两边各有盲区（SBP 漏 SpriteAtlas/变体关系，AssetDatabase 漏序列化隐式依赖），
                // 过渡期清单依赖取并集，保证运行时预加载不漏；差异仍记日志供阶段 C 收敛。
                if (sbpDependencyResolver != null)
                {
                    string[] sbpDependencies = CollectFlatFromResolver(registeredName, sbpDependencyResolver);
                    if (!SameBundleSet(info.Dependencies, sbpDependencies))
                    {
                        DiffBundleSets(
                            info.Dependencies,
                            sbpDependencies,
                            out List<string> dbOnly,
                            out List<string> sbpOnly);

                        dependencyDiffCount++;
                        dependencyDbOnlyTotal += dbOnly.Count;
                        dependencySbpOnlyTotal += sbpOnly.Count;

                        if (dependencyDiffSamples.Count < MaxDependencyDiffSamples)
                        {
                            dependencyDiffSamples.Add(string.Format(
                                "{0}\n    SBP 多 {1} 项{2}\n    DB  多 {3} 项{4}",
                                bundleKey,
                                sbpOnly.Count,
                                FormatDependencySample(sbpOnly, 6),
                                dbOnly.Count,
                                FormatDependencySample(dbOnly, 6)));
                        }
                    }

                    // 并集：避免任一来源漏报导致运行时缺依赖。
                    info.Dependencies = UnionSorted(info.Dependencies, sbpDependencies);
                }

                generatedManifest.AssetBundles.Add(info);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        LogDependencySourceDiff(
            dependencyDiffCount,
            dependencySbpOnlyTotal,
            dependencyDbOnlyTotal,
            dependencyDiffSamples,
            sbpDependencyResolver != null);

        LogMissingOnDiskSummary(missingOnDisk);

        return new ManifestBuildData
        {
            Manifest = generatedManifest,
            MissingOnDisk = missingOnDisk,
        };
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

    /// <summary>
    /// 清空平台输出目录（带安全校验，拒绝盘符根 / 工程根 / Assets 非 StreamingAssets 路径）。
    /// <para>从 <see cref="BuildAssetBundles"/> 的 clearFolders 分支拆出，供 CleanOutputStep 复用。</para>
    /// </summary>
    public static bool TryCleanOutputDirectory(string outputPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(outputPath))
        {
            error = "输出目录为空";
            return false;
        }

        if (!Directory.Exists(outputPath))
        {
            return true;
        }

        // 先记下清单版本，再删目录。失败的这次打包不会把下次版本打回 1。
        AssetBundleBuildSettings.RememberManifestVersionFromDirectory(outputPath);

        if (!IsSafeToDeleteDirectory(outputPath))
        {
            error = "输出目录过于危险，已拒绝清空: " + outputPath;
            return false;
        }

        Directory.Delete(outputPath, true);
        return true;
    }
    /// <summary>
    /// 已在 AssetDatabase 登记且仍有关联资源，但输出目录找不到对应 AB 文件。
    /// 不含「无资源引用的历史包名」（已由 RemoveUnusedAssetBundleNames 清理）。
    /// </summary>
    /// <summary>
    /// 用「每个包的直接依赖」解析器递归展开成扁平集合（去重、排序）。
    /// <para>阶段 A 用 SBP 的 BundleInfos 做对比；阶段 C 可直接作为清单依赖来源。</para>
    /// </summary>
    private static string[] CollectFlatFromResolver(string bundleName, Func<string, string[]> resolve)
    {
        if (resolve == null)
        {
            return Array.Empty<string>();
        }

        string self = NormalizeBundleName(bundleName);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        visited.Add(bundleName);
        stack.Push(bundleName);

        while (stack.Count > 0)
        {
            string current = stack.Pop();
            string[] direct = resolve(current);
            if (direct == null)
            {
                continue;
            }

            for (int i = 0; i < direct.Length; i++)
            {
                string dependency = NormalizeBundleName(direct[i]);
                if (string.IsNullOrEmpty(dependency)
                    || string.Equals(dependency, self, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!unique.Add(dependency))
                {
                    continue;
                }

                if (visited.Add(dependency))
                {
                    stack.Push(dependency);
                }
            }
        }

        var result = new List<string>(unique);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    private static bool SameBundleSet(string[] a, string[] b)
    {
        string[] left = a ?? Array.Empty<string>();
        string[] right = b ?? Array.Empty<string>();
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void LogDependencySourceDiff(
        int diffCount,
        int sbpOnlyTotal,
        int dbOnlyTotal,
        List<string> samples,
        bool resolverUsed)
    {
        if (!resolverUsed)
        {
            return;
        }

        if (diffCount == 0)
        {
            Debug.Log("[AB Build] 依赖来源对比：AssetDatabase 与 SBP BundleInfos 完全一致。");
            return;
        }

        string detail = samples != null && samples.Count > 0
            ? "\n  " + string.Join("\n  ", samples)
            : string.Empty;
        Debug.LogWarning(string.Format(
            "[AB Build] 依赖来源对比：{0} 个包的依赖不一致"
            + "（SBP 多出 {1} 项，AssetDatabase 多出 {2} 项；清单已取两边并集）。{3}",
            diffCount,
            sbpOnlyTotal,
            dbOnlyTotal,
            detail));
    }

    /// <summary>两个扁平依赖集合的并集（去重、排序）。</summary>
    private static string[] UnionSorted(string[] left, string[] right)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (left != null)
        {
            for (int i = 0; i < left.Length; i++)
            {
                unique.Add(left[i]);
            }
        }

        if (right != null)
        {
            for (int i = 0; i < right.Length; i++)
            {
                unique.Add(right[i]);
            }
        }

        var result = new List<string>(unique);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    /// <summary>比较两个扁平依赖集合，分别得到「只有 DB 有」与「只有 SBP 有」。</summary>
    private static void DiffBundleSets(
        string[] databaseDependencies,
        string[] sbpDependencies,
        out List<string> dbOnly,
        out List<string> sbpOnly)
    {
        var dbSet = new HashSet<string>(
            databaseDependencies ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var sbpSet = new HashSet<string>(
            sbpDependencies ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        dbOnly = new List<string>();
        sbpOnly = new List<string>();
        foreach (string item in dbSet)
        {
            if (!sbpSet.Contains(item))
            {
                dbOnly.Add(item);
            }
        }

        foreach (string item in sbpSet)
        {
            if (!dbSet.Contains(item))
            {
                sbpOnly.Add(item);
            }
        }

        dbOnly.Sort(StringComparer.Ordinal);
        sbpOnly.Sort(StringComparer.Ordinal);
    }

    private static string FormatDependencySample(List<string> items, int max)
    {
        if (items == null || items.Count == 0)
        {
            return string.Empty;
        }

        int count = Math.Min(max, items.Count);
        string sample = string.Join(", ", items.GetRange(0, count));
        return items.Count > count
            ? "（如 " + sample + " …）"
            : "（" + sample + "）";
    }

    /// <summary>
    /// 取某个包的直接 + 间接依赖（扁平、去重、排序）。
    /// <para>
    /// <c>GetAssetBundleDependencies(name, true)</c> 本身已是递归的扁平结果，
    /// 不要再对每个依赖递归一次（旧实现是 O(N×D²)，包多时清单阶段明显变慢）。
    /// </para>
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

        // 这些包「有资源登记但没构建出文件」，会被排除在清单之外。
        // 用 LogError 而不是 LogWarning：否则打包仍显示"成功"，运行时才以
        // "bundle not found" 的形式暴露，极难定位。
        Debug.LogError(string.Format(
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
            || file.EndsWith(".txt")
            || file.EndsWith(".sig");
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

        // 必须位于工程目录内：防止 AB_OutputRoot 填成工程外路径后 Directory.Delete 误删。
        if (!IsSameOrUnder(fullPath, projectRoot))
        {
            return false;
        }

        // 工程根下的这些目录一律拒绝：它们是 Unity / 工具链的缓存与配置，
        // 误删会导致编辑器重建、丢缓存，甚至丢工程设置（旧实现只拦了 Assets，Library 可被删）。
        string[] protectedDirs = { "Library", "Temp", "Logs", "obj", "Packages", "ProjectSettings", ".git" };
        for (int i = 0; i < protectedDirs.Length; i++)
        {
            if (IsSameOrUnder(fullPath, Path.Combine(projectRoot, protectedDirs[i])))
            {
                Debug.LogError("[AB Build] 拒绝删除受保护目录: " + fullPath);
                return false;
            }
        }

        // Assets 下只允许动 StreamingAssets/AssetBundles（CopyToStreamingAssets 的目标）；
        // Assets 本身与资源目录一律拒绝。
        string assetsRoot = Path.GetFullPath(Application.dataPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsSameOrUnder(fullPath, assetsRoot))
        {
            string streamingBundleRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "AssetBundles"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return IsSameOrUnder(fullPath, streamingBundleRoot);
        }

        return true;
    }

    /// <summary>child 是否等于 parent 或位于其下；按目录边界比较，避免 CarrotFantasy2 命中 CarrotFantasy。</summary>
    private static bool IsSameOrUnder(string child, string parent)
    {
        if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent))
        {
            return false;
        }

        if (string.Equals(child, parent, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return child.StartsWith(parent + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase)
               || child.StartsWith(parent + Path.AltDirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase);
    }
}

public enum CompressionType
{
    StandardCompression,
    ChunkBasedCompression,
    NoCompression
}
