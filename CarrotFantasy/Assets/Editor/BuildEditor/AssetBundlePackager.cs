using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    /// <summary>Build asset bundles to output path（平台子目录，如 .../StandaloneWindows）。</summary>
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

        HashSet<string> registeredBundles = CollectRegisteredBundleNames();

        CustomManifest generatedManifest = new CustomManifest
        {
            AppVersion = Application.version,
            BuildTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            buildTime = System.DateTime.Now.Ticks,
            ManifestVersion = versionNumber,
            CompressedFormat = compressedFormat,
        };

        string[] bundleFiles = Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < bundleFiles.Length; i++)
            {
                string file = bundleFiles[i];
                if (ShouldSkipManifestFile(file))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar("生成AB清单", Path.GetFileName(file), (i + 1f) / bundleFiles.Length);

                string bundleName = GetBundlePath(bundlePath, file);
                if (!registeredBundles.Contains(bundleName))
                {
                    continue;
                }

                CustomAssetBundleInfo info = new CustomAssetBundleInfo
                {
                    AssetName = Path.GetFileName(file),
                    BundleName = bundleName,
                    Size = new FileInfo(file).Length,
                    Hash = MD5Checker.ComputeFileMD5(file),
                };

                HashSet<string> processedBundles = new HashSet<string>();
                GenerateFlatDependencyList(bundleName, 0, processedBundles);
                info.Dependencies = processedBundles.ToArray();

                generatedManifest.AssetBundles.Add(info);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string manifestJson = JsonUtility.ToJson(generatedManifest, true);
        string manifestPath = Path.Combine(bundlePath, "custom_manifest.json");
        File.WriteAllText(manifestPath, manifestJson);

        string versionPath = Path.Combine(bundlePath, "version.txt");
        File.WriteAllText(versionPath, generatedManifest.BuildTime);

        AssetDatabase.Refresh();
        if (showSuccessDialog)
        {
            EditorUtility.DisplayDialog("成功", "清单文件已生成！", "确定");
        }

        Debug.Log($"[AB Build] custom_manifest.json 已生成，共 {generatedManifest.AssetBundles.Count} 个 AB 包。");
        return generatedManifest;
    }

    private static HashSet<string> CollectRegisteredBundleNames()
    {
        string[] names = AssetDatabase.GetAllAssetBundleNames();
        var set = new HashSet<string>();
        for (int i = 0; i < names.Length; i++)
        {
            set.Add(names[i].ToLower().Replace('\\', '/'));
        }

        return set;
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
