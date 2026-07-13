using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 清理本地下载的 AB 包、清单、临时文件及内存中的 AB 缓存。
/// </summary>
public static class AssetBundleCacheCleaner
{
    private const string LogTag = "AssetBundleCacheCleaner";

    public static void ClearAll()
    {
        AssetBundleDownloader.Instance?.EndDownload();
        ClearDownloadedFiles();
        ClearLocalManifest();
        ClearTempDownloadFiles();

        if (AssetBundleManager.Instance != null)
        {
            AssetBundleManager.Instance.ClearRuntimeCache();
        }

        Debug.Log($"[{LogTag}] 缓存已清理");
    }

    static void ClearDownloadedFiles()
    {
        string downloadDir = AssetBundlePathHelper.GetPersistentDownloadDirectory();
        if (!Directory.Exists(downloadDir))
        {
            return;
        }

        try
        {
            Directory.Delete(downloadDir, true);
            Directory.CreateDirectory(downloadDir);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 清理 AB 下载目录失败: {e.Message}");
        }
    }

    static void ClearLocalManifest()
    {
        string manifestPath = AssetBundlePathHelper.GetPersistentManifestPath();
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            File.Delete(manifestPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 清理本地清单失败: {e.Message}");
        }
    }

    static void ClearTempDownloadFiles()
    {
        string tempRoot = Application.temporaryCachePath;
        if (!Directory.Exists(tempRoot))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(tempRoot, "*.temp"))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{LogTag}] 清理临时文件失败: {file}, {e.Message}");
            }
        }
    }
}
