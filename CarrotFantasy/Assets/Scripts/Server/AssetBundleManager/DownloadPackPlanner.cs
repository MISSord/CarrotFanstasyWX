using System;
using System.Collections.Generic;

/// <summary>
/// 根据待下载 / 待更新 AB，从清单 DownloadPacks 计算需要拉取的合并包。
///
/// 调用时机（关键）：必须在 UpdateCheckResult.customManifest 已赋值为远程清单之后。
/// 命中规则：Pack.BundleNames 中任一属于 neededBundles → 整 Pack 加入 packsToDownload。
/// 副作用：若命中至少一个 Pack，用 Pack 总大小覆盖 totalDownloadSize（确认弹窗显示的是 Pack 流量）。
/// DownloadPacks 为空时不做处理，Downloader 回退为按单个 AB 下载。
/// </summary>
public static class DownloadPackPlanner
{
    public static bool HasDownloadPacks(CustomManifest manifest)
    {
        return manifest?.DownloadPacks != null && manifest.DownloadPacks.Count > 0;
    }

    public static void ApplyPackDownloadPlan(UpdateCheckResult result)
    {
        result.packsToDownload.Clear();
        // 依赖 result.customManifest；调用方须先赋值远程清单。
        if (result == null || !HasDownloadPacks(result.customManifest))
        {
            return;
        }

        HashSet<string> neededBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.bundlesToDownload.Count; i++)
        {
            neededBundles.Add(result.bundlesToDownload[i].BundleName);
        }

        for (int i = 0; i < result.bundlesToUpdate.Count; i++)
        {
            neededBundles.Add(result.bundlesToUpdate[i].BundleName);
        }

        if (neededBundles.Count == 0)
        {
            result.totalDownloadSize = 0;
            return;
        }

        List<DownloadPackInfo> packs = result.customManifest.DownloadPacks;
        long packTotalSize = 0;
        for (int i = 0; i < packs.Count; i++)
        {
            DownloadPackInfo pack = packs[i];
            if (pack?.BundleNames == null)
            {
                continue;
            }

            bool needsPack = false;
            for (int j = 0; j < pack.BundleNames.Length; j++)
            {
                if (neededBundles.Contains(pack.BundleNames[j]))
                {
                    needsPack = true;
                    break;
                }
            }

            if (needsPack)
            {
                result.packsToDownload.Add(pack);
                packTotalSize += pack.PackSize;
            }
        }

        if (result.packsToDownload.Count > 0)
        {
            // 合并下载时 UI 展示 Pack 流量，而非单个 AB Size 之和。
            result.totalDownloadSize = packTotalSize;
        }
    }
}
