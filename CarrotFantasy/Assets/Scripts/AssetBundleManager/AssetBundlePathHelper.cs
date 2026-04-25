using System.IO;
using UnityEngine;

/// <summary>
/// AssetBundle路径管理工具
/// 提供统一的路径获取方法，支持多平台
/// </summary>
public static class AssetBundlePathHelper
{
    public static string ServerDownloadURL = "file://D:/Work/MinecraftAB/{0}";
    public static string localSavePath = "DownloadedAssetBundles";

    public static string GetServerLoadUrl()
    {
        return string.Format(ServerDownloadURL, GetRuntimePlatformFolder());
    }

    /// <summary>
    /// 获取本地LZ4格式AB包路径
    /// </summary>
    public static string GetLocalLZ4Path(string bundleName)
    {
        return Application.persistentDataPath + "/" + localSavePath + "/" + bundleName;
    }

    //    /// <summary>
    //    /// 获取AssetBundle根目录
    //    /// </summary>
    //    public static string GetAssetBundleRootPath()
    //    {
    //#if UNITY_EDITOR
    //        // 在编辑器中，使用项目目录
    //        return Path.Combine(Application.dataPath, "..", localSavePath);
    //#elif UNITY_STANDALONE
    //        return Path.Combine(Application.dataPath, "..", localSavePath);
    //#elif UNITY_ANDROID || UNITY_IOS
    //        return Path.Combine(Application.persistentDataPath, localSavePath);
    //#else
    //        return Path.Combine(Application.streamingAssetsPath, localSavePath);
    //#endif
    //    }

    /// <summary>
    /// 获取运行时加载路径
    /// </summary>
    public static string GetRuntimeLoadPath(string bundleName)
    {
        string platformFolder = GetRuntimePlatformFolder();
        string fileName = GetBundleFileName(bundleName);

        // 优先检查热更新路径（持久化数据路径）
        string persistentPath = Path.Combine(Application.persistentDataPath, localSavePath, platformFolder, fileName);
        if (File.Exists(persistentPath))
        {
            return persistentPath;
        }

        // 使用内置资源路径
        string streamingPath = Path.Combine(Application.streamingAssetsPath, localSavePath, platformFolder, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android平台下，StreamingAssets中的文件不能直接使用File.Exists检查
        return Path.Combine(Application.streamingAssetsPath, "AssetBundles", platformFolder, fileName);
#else
        if (File.Exists(streamingPath))
        {
            return streamingPath;
        }
#endif

        return streamingPath;
    }

    /// <summary>
    /// 获取当前运行平台的文件夹名称
    /// </summary>
    public static string GetRuntimePlatformFolder()
    {
#if UNITY_STANDALONE_WIN
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

    /// <summary>
    /// 获取完整的Bundle文件名
    /// </summary>
    public static string GetBundleFileName(string bundleName)
    {
        // Unity会自动添加平台后缀，但基础名称保持不变
        return bundleName.ToLower();
    }

    /// <summary>
    /// 检查文件在持久化路径中是否存在
    /// </summary>
    public static bool ExistsInPersistentData(string bundleName)
    {
        string path = GetLocalLZ4Path(bundleName);
        return File.Exists(path);
    }

    ///// <summary>
    ///// 拷贝文件到持久化数据路径（用于热更新）
    ///// </summary>
    //public static void CopyToPersistentPath(string sourcePath, string bundleName)
    //{
    //    string targetDir = Path.Combine(Application.persistentDataPath, "AssetBundles", GetRuntimePlatformFolder());
    //    string targetPath = Path.Combine(targetDir, GetBundleFileName(bundleName));

    //    if (!Directory.Exists(targetDir))
    //    {
    //        Directory.CreateDirectory(targetDir);
    //    }

    //    File.Copy(sourcePath, targetPath, true);
    //    Debug.Log($"已拷贝到持久化路径: {targetPath}");
    //}

    ///// <summary>
    ///// 获取所有可用的AssetBundle路径（用于调试）
    ///// </summary>
    //public static void PrintAllPaths(string bundleName = "")
    //{
    //    Debug.Log("=== AssetBundle路径信息 ===");
    //    Debug.Log($"数据路径: {Application.dataPath}");
    //    Debug.Log($"持久化数据路径: {Application.persistentDataPath}");
    //    Debug.Log($"StreamingAssets路径: {Application.streamingAssetsPath}");

    //    if (!string.IsNullOrEmpty(bundleName))
    //    {
    //        string platformFolder = GetRuntimePlatformFolder();
    //        string fileName = GetBundleFileName(bundleName);

    //        string persistentPath = Path.Combine(Application.persistentDataPath, "AssetBundles", platformFolder, fileName);
    //        string streamingPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles", platformFolder, fileName);

    //        Debug.Log($"持久化路径: {persistentPath} (存在: {File.Exists(persistentPath)})");
    //        Debug.Log($"Streaming路径: {streamingPath} (存在: {File.Exists(streamingPath)})");
    //    }
    //}
}