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

        // 必须先卸载再删文件。反过来做的话，AB 还持着文件句柄（LoadFromFile 映射中），
        // Windows 上 Directory.Delete 会抛 IOException 并留下"半删"的目录，
        // 而此时清单已经被删掉，状态就脏了。
        if (AssetBundleManager.Instance != null)
        {
            AssetBundleManager.Instance.ClearRuntimeCache();
        }

        ClearDownloadedFiles();
        ClearLocalManifest();
        ClearTempDownloadFiles();

        Debug.Log($"[{LogTag}] 缓存已清理");
    }

    /// <summary>
    /// 客户端整包升级后重置：旧版本的 AB / 热更 DLL / 本地清单全部作废。
    /// <para>
    /// 返回 false 表示**没有清理干净**（文件被占用、权限不足等）。
    /// 调用方据此决定是否写入版本标记——清理失败还写标记的话，下次启动不会再尝试清理，
    /// 旧 AB 会一直盖住包内基线。
    /// </para>
    /// </summary>
    public static bool ResetDownloadedAssetsForAppVersionChange()
    {
        bool filesCleared = ClearDownloadedFiles();
        bool manifestCleared = ClearLocalManifest();

        if (filesCleared && manifestCleared)
        {
            Debug.Log($"[{LogTag}] 客户端版本变化，已清空旧版本 AB 缓存");
            return true;
        }

        Debug.LogWarning($"[{LogTag}] 客户端版本变化，但缓存未清理干净（files={filesCleared}, manifest={manifestCleared}）");
        return false;
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

    static bool ClearDownloadedFiles()
    {
        string downloadDir = AssetBundlePathHelper.GetPersistentDownloadDirectory();
        if (!Directory.Exists(downloadDir))
        {
            return true;
        }

        try
        {
            Directory.Delete(downloadDir, true);
            Directory.CreateDirectory(downloadDir);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 清理 AB 下载目录失败: {e.Message}");
            return false;
        }
    }

    static bool ClearLocalManifest()
    {
        string manifestPath = AssetBundlePathHelper.GetPersistentManifestPath();
        if (!File.Exists(manifestPath))
        {
            return true;
        }

        try
        {
            File.Delete(manifestPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 清理本地清单失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清理下载 / 解包过程中产生的临时文件。真实产物分布：
    ///   &lt;persistent&gt;/&lt;bundle&gt;.download   下载暂存（与正式文件同目录）
    ///   &lt;temp&gt;/&lt;bundle&gt;.pack.temp        单包解包暂存
    ///   &lt;temp&gt;/pack_&lt;name&gt;.zip           Pack 压缩包
    ///   &lt;temp&gt;/pack_extract_&lt;name&gt;/       Pack 解压根目录
    /// 原实现只删顶层 <c>*.temp</c>，上面这些大件全在盲区
    /// （一次中断的 Pack 更新可能残留几百 MB）。
    /// </summary>
    static void ClearTempDownloadFiles()
    {
        string tempRoot = Application.temporaryCachePath;

        // 解包暂存 / 下载暂存 / Pack 压缩包
        ClearFilesByPattern(tempRoot, "*.pack.temp", SearchOption.AllDirectories);
        ClearFilesByPattern(tempRoot, "*.temp", SearchOption.AllDirectories);
        ClearFilesByPattern(tempRoot, "pack_*.zip", SearchOption.TopDirectoryOnly);

        if (!Directory.Exists(tempRoot))
        {
            return;
        }

        // 解压根目录整目录删除
        try
        {
            foreach (string dir in Directory.GetDirectories(tempRoot, "pack_extract_*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{LogTag}] 清理解压目录失败: {dir}, {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 枚举解压目录失败: {e.Message}");
        }
    }

    static void ClearFilesByPattern(string root, string pattern, SearchOption option)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.GetFiles(root, pattern, option))
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
        catch (Exception e)
        {
            Debug.LogWarning($"[{LogTag}] 枚举临时文件失败: {root} / {pattern}, {e.Message}");
        }
    }
}
