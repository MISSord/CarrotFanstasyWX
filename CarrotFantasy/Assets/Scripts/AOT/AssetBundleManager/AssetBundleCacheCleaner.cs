using System;
using System.Collections.Generic;
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

    /// <summary>客户端整包升级后重置：旧版本的 AB / 热更 DLL / 本地清单全部作废。</summary>
    public static void ResetDownloadedAssetsForAppVersionChange()
    {
        ClearDownloadedFiles();
        ClearLocalManifest();
        Debug.Log($"[{LogTag}] 客户端版本变化，已清空旧版本 AB 缓存");
    }

    /// <summary>
    /// 以远端清单为准，删除持久化目录里已经不在清单中的 AB（旧图集、旧热更 DLL 等）。
    /// 只处理下载目录，不动 StreamingAssets、也不动暂存文件。
    /// </summary>
    public static int CleanObsoleteBundles(CustomManifest manifest)
    {
        if (manifest?.AssetBundles == null)
        {
            return 0;
        }

        string root = AssetBundlePathHelper.GetPersistentDownloadDirectory();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return 0;
        }

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo info = manifest.AssetBundles[i];
            if (info != null && !string.IsNullOrEmpty(info.BundleName))
            {
                keep.Add(info.BundleName.Replace('\\', '/'));
            }
        }

        if (manifest.DownloadPacks != null)
        {
            for (int i = 0; i < manifest.DownloadPacks.Count; i++)
            {
                DownloadPackInfo pack = manifest.DownloadPacks[i];
                if (pack != null && !string.IsNullOrEmpty(pack.PackFileName))
                {
                    keep.Add(pack.PackFileName.Replace('\\', '/'));
                }
            }
        }

        int removed = 0;
        string rootFull = Path.GetFullPath(root);
        foreach (string file in Directory.GetFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(rootFull.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            if (keep.Contains(relative))
            {
                continue;
            }

            if (relative.EndsWith(".download", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".new", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{LogTag}] 删除过期 AB 失败: {relative}, {e.Message}");
            }
        }

        if (removed > 0)
        {
            Debug.Log($"[{LogTag}] 已清理 {removed} 个远端清单中不存在的 AB");
        }

        return removed;
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
