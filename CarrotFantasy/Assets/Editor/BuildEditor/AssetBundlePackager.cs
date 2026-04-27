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
        BuildTarget.iOS
    };

    public static readonly string[] platformNames = {
        "Windows 32-bit",
        "Windows 64-bit",
        "macOS",
        "Android",
        "iOS"
    };

    /// <summary>Build asset bundles to output path.</summary>
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
                Debug.LogError("\u8f93\u51fa\u8def\u5f84\u4e0d\u80fd\u4e3a\u7a7a\uff01");
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
                Debug.Log("\u5df2\u6e05\u7a7a\u8f93\u51fa\u6587\u4ef6\u5939: " + outputPath);
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                Debug.Log("\u5df2\u521b\u5efa\u8f93\u51fa\u76ee\u5f55: " + outputPath);
            }

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, compression, buildTarget);
            if (manifest == null)
            {
                Debug.LogError("BuildPipeline.BuildAssetBundles 返回空结果。");
                return false;
            }

            Debug.Log($"AssetBundle \u5df2\u751f\u6210\u3002\u8def\u5f84: {outputPath} \u5e73\u53f0: {buildTarget}");

            if (copyToStreamingAssets)
            {
                string streamingAssetsPath = Application.streamingAssetsPath + "/AssetBundles";
                if (Directory.Exists(streamingAssetsPath))
                {
                    Directory.Delete(streamingAssetsPath, true);
                }

                FileUtil.CopyFileOrDirectory(outputPath, streamingAssetsPath);
                Debug.Log("\u5df2\u590d\u5236\u5230 StreamingAssets: " + streamingAssetsPath);

                AssetDatabase.Refresh();
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("AssetBundle \u6253\u5305\u5931\u8d25: " + e.Message);
            return false;
        }
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
        repath = repath.Replace(outputPath + "/", "");
        return repath.ToLower();
    }

    public static CustomManifest GenerateManifest(string bundlePath, BuildTarget target, int VersionNumber = 1, int CompressedFormat = 0)
    {
        bundlePath = bundlePath + "/" + AssetBundlePackager.GetPlatformFolder(target);
        if (!Directory.Exists(bundlePath))
        {
            EditorUtility.DisplayDialog("\u9519\u8bef", "AB \u5305\u76ee\u5f55\u4e0d\u5b58\u5728\uff01", "\u786e\u5b9a");
            return null;
        }

        CustomManifest generatedManifest = new CustomManifest
        {
            AppVersion = Application.version,
            BuildTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            buildTime = System.DateTime.Now.Ticks,
            ManifestVersion = VersionNumber,
            CompressedFormat = CompressedFormat,
        };

        string[] bundleFiles = Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < bundleFiles.Length; i++)
            {
                string file = bundleFiles[i];
                if (file.EndsWith(".meta") || file.EndsWith(".manifest") || file.EndsWith(".json") || file.EndsWith(".txt"))
                    continue;

                EditorUtility.DisplayProgressBar("生成AB清单", Path.GetFileName(file), (i + 1f) / bundleFiles.Length);

                string BundleName = AssetBundlePackager.GetBundlePath(bundlePath, file);
                CustomAssetBundleInfo info = new CustomAssetBundleInfo
                {
                    AssetName = Path.GetFileName(file),
                    BundleName = BundleName,
                    Size = new FileInfo(file).Length,
                    Hash = MD5Checker.ComputeFileMD5(file),
                };

                // 统一通过 AssetDatabase 读取依赖，避免多套来源带来的不一致。
                HashSet<string> processedBundles = new HashSet<string>();
                GenerateFlatDependencyList(BundleName, 0, processedBundles);
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
        EditorUtility.DisplayDialog("\u6210\u529f", "\u6e05\u5355\u6587\u4ef6\u5df2\u751f\u6210\uff01", "\u786e\u5b9a");

        return generatedManifest;
    }

    private static void GenerateFlatDependencyList(string abName, int depth, HashSet<string> processedBundles)
    {
        if (processedBundles.Contains(abName))
        {
            return;
        }

        if(depth != 0) processedBundles.Add(abName);

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
            return false;

        if (string.Equals(fullPath, rootPath, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullPath, projectRoot, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}

public enum CompressionType
{
    StandardCompression,
    ChunkBasedCompression,
    NoCompression
}
