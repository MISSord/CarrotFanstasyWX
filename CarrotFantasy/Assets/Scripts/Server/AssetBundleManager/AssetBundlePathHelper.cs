using System;
using System.IO;
using UnityEngine;

/// <summary>
/// AssetBundle 路径管理：持久化目录、StreamingAssets、热更 CDN 根 URL。
/// CDN 模板由 Editor 写入 StreamingAssets/ab_runtime_config.json。
/// </summary>
public static class AssetBundlePathHelper
{
    private const string DefaultServerDownloadUrlTemplate = "file:///{0}";

    public static string ServerDownloadURL = DefaultServerDownloadUrlTemplate;
    public static string localSavePath = "DownloadedAssetBundles";

    private static bool initialized;

    /// <summary>启动时读取 StreamingAssets 中的 ab_runtime_config.json。</summary>
    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        TryLoadRuntimeConfig();
    }

    public static string GetServerLoadUrl()
    {
        Initialize();
        return string.Format(ServerDownloadURL, GetRuntimePlatformFolder());
    }

    public static string GetLocalLZ4Path(string bundleName)
    {
        string normalizedBundleName = GetBundleFileName(bundleName).Replace('\\', '/');
        return Path.Combine(GetPersistentDownloadDirectory(), normalizedBundleName);
    }

    /// <summary>PC / 运行时 AB 热更下载目录（persistentDataPath/DownloadedAssetBundles）。</summary>
    public static string GetPersistentDownloadDirectory()
    {
        Initialize();
        return Path.Combine(Application.persistentDataPath, localSavePath);
    }

    /// <summary>本地缓存的 custom_manifest.json 路径。</summary>
    public static string GetPersistentManifestPath()
    {
        return Path.Combine(Application.persistentDataPath, "custom_manifest.json");
    }

    /// <summary>
    /// Editor / file:// 配置下的 Build 输出 AB 路径（与 ab_runtime_config 的 serverDownloadUrlTemplate 一致）。
    /// </summary>
    public static string GetBuildOutputBundlePath(string bundleName)
    {
        Initialize();

        string buildRoot = ResolveLocalFilePath(GetServerLoadUrl());
        if (string.IsNullOrEmpty(buildRoot))
        {
            return string.Empty;
        }

        string relativePath = GetBundleFileName(bundleName).Replace('\\', '/');
        return Path.Combine(buildRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string GetRuntimeLoadPath(string bundleName)
    {
        Initialize();

        string fileName = GetBundleFileName(bundleName);

        string persistentPath = GetLocalLZ4Path(fileName);
        if (File.Exists(persistentPath))
        {
            return persistentPath;
        }

#if UNITY_EDITOR
        string buildPath = GetBuildOutputBundlePath(bundleName);
        if (!string.IsNullOrEmpty(buildPath) && File.Exists(buildPath))
        {
            return buildPath;
        }
#endif

        string platformFolder = GetRuntimePlatformFolder();
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", platformFolder, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        return streamingPath;
#else
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif

        return streamingPath;
    }

    public static string GetRuntimePlatformFolder()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "WebGL";
#elif UNITY_STANDALONE_WIN
        return "StandaloneWindows";
#elif UNITY_STANDALONE_OSX
        return "StandaloneOSX";
#elif UNITY_ANDROID
        return "Android";
#elif UNITY_IOS
        return "iOS";
#else
        return "Unknown";
#endif
    }

    public static string GetBundleFileName(string bundleName)
    {
        return bundleName.ToLower();
    }

    public static bool ExistsInPersistentData(string bundleName)
    {
        string path = GetLocalLZ4Path(bundleName);
        return File.Exists(path);
    }

    /// <summary>持久化目录、Build 输出或 StreamingAssets 中是否存在该 AB 包。</summary>
    public static bool IsBundleAvailableAtRuntime(string bundleName)
    {
        Initialize();

        string fileName = GetBundleFileName(bundleName);
        if (File.Exists(GetLocalLZ4Path(fileName)))
        {
            return true;
        }

#if UNITY_EDITOR
        string buildPath = GetBuildOutputBundlePath(bundleName);
        if (!string.IsNullOrEmpty(buildPath) && File.Exists(buildPath))
        {
            return true;
        }
#endif

        string streamingPath = Path.Combine(
            Application.streamingAssetsPath,
            "AssetBundles",
            GetRuntimePlatformFolder(),
            fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return File.Exists(streamingPath);
#endif
    }

    public static string GetStreamingAssetsManifestPath()
    {
        return Path.Combine(
            Application.streamingAssetsPath,
            "AssetBundles",
            GetRuntimePlatformFolder(),
            "custom_manifest.json");
    }

    /// <summary>解析 file:// URL 或普通路径为本地文件路径。</summary>
    public static string ResolveLocalFilePath(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            return string.Empty;
        }

        string normalized = pathOrUrl.Replace("\\", "/");
        if (normalized.StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized).LocalPath;
        }

        return pathOrUrl;
    }

    static void TryLoadRuntimeConfig()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, AssetBundleRuntimeConfig.FileName);
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android StreamingAssets 不可直接 File.Exists；保留默认 URL，由发布配置或后续扩展 UWR 读取。
        return;
#else
        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            AssetBundleRuntimeConfig config = JsonUtility.FromJson<AssetBundleRuntimeConfig>(json);
            if (config == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(config.serverDownloadUrlTemplate))
            {
                ServerDownloadURL = config.serverDownloadUrlTemplate;
            }

            if (!string.IsNullOrEmpty(config.localSavePath))
            {
                localSavePath = config.localSavePath;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[AssetBundlePathHelper] 读取运行时配置失败: " + ex.Message);
        }
#endif
    }
}
