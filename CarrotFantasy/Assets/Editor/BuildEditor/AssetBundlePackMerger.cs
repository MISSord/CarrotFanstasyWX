using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

/// <summary>
/// 构建后将多个 AB 合并为 ZIP Pack，并写入 CustomManifest.DownloadPacks。
///
/// 分组策略（见 PackGroupingDefaults）：
/// 1. Boot 包 → pack_boot_*（启动必需，优先单独成组）
/// 2. 超大包 / 匹配 StandalonePatterns → pack_single_*（一包一 ZIP，避免拖累小更新）
/// 3. 其余按 BundleName 前两级目录分桶 → pack_{bucket}_*，再按 MaxPackSize 装箱
///
/// 运行时：UpdateChecker 算出待更 AB 后，DownloadPackPlanner 命中任一 BundleNames 即整 Pack 下载。
/// </summary>
public static class AssetBundlePackMerger
{
    /// <summary>
    /// 清空并重建 packs/，为每个装箱结果写 ZIP + DownloadPackInfo（Size/MD5/内含 BundleNames）。
    /// </summary>
    public static void BuildPacks(CustomManifest manifest, string platformBundlePath)
    {
        if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
        {
            return;
        }

        string packsDir = Path.Combine(platformBundlePath, PackGroupingDefaults.PacksFolderName);
        if (Directory.Exists(packsDir))
        {
            Directory.Delete(packsDir, true);
        }

        Directory.CreateDirectory(packsDir);

        Dictionary<string, CustomAssetBundleInfo> bundleMap = new Dictionary<string, CustomAssetBundleInfo>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.AssetBundles.Count; i++)
        {
            CustomAssetBundleInfo bundle = manifest.AssetBundles[i];
            bundleMap[bundle.BundleName] = bundle;
        }

        List<PackBuildGroup> groups = CreateGroups(manifest.AssetBundles);
        manifest.DownloadPacks = new List<DownloadPackInfo>();

        for (int i = 0; i < groups.Count; i++)
        {
            PackBuildGroup group = groups[i];
            // 同组内再按大小装箱，避免单个 ZIP 过大。
            List<List<CustomAssetBundleInfo>> bins = PackBins(group.Bundles);
            for (int binIndex = 0; binIndex < bins.Count; binIndex++)
            {
                string packName = group.PackNamePrefix + "_" + (binIndex + 1).ToString("000");
                string packFileName = packName + PackGroupingDefaults.PackFileExtension;
                string zipPath = Path.Combine(packsDir, packFileName);

                List<CustomAssetBundleInfo> binBundles = bins[binIndex];
                CreatePackZip(zipPath, platformBundlePath, binBundles);

                FileInfo zipInfo = new FileInfo(zipPath);
                DownloadPackInfo packInfo = new DownloadPackInfo
                {
                    PackName = packName,
                    // 相对平台目录的路径，运行时拼 CDN：{platformUrl}/packs/xxx.zip
                    PackFileName = PackGroupingDefaults.PacksFolderName + "/" + packFileName,
                    PackSize = zipInfo.Length,
                    PackHash = MD5Checker.ComputeFileMD5(zipPath),
                    BundleNames = binBundles.Select(b => b.BundleName).ToArray(),
                };
                manifest.DownloadPacks.Add(packInfo);
            }
        }

        Debug.Log(string.Format(
            "[PackMerger] 已生成 {0} 个下载 Pack，覆盖 {1} 个 AB。",
            manifest.DownloadPacks.Count,
            manifest.AssetBundles.Count));
    }

    /// <summary>Boot → 目录桶 → 独立大包，三类互斥。</summary>
    static List<PackBuildGroup> CreateGroups(List<CustomAssetBundleInfo> bundles)
    {
        var bootBundles = new List<CustomAssetBundleInfo>();
        var standaloneBundles = new List<CustomAssetBundleInfo>();
        var bucketBundles = new Dictionary<string, List<CustomAssetBundleInfo>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < bundles.Count; i++)
        {
            CustomAssetBundleInfo bundle = bundles[i];
            if (PackGroupingDefaults.IsBootBundle(bundle.BundleName))
            {
                bootBundles.Add(bundle);
                continue;
            }

            if (PackGroupingDefaults.ShouldStandalone(bundle))
            {
                standaloneBundles.Add(bundle);
                continue;
            }

            string bucket = PackGroupingDefaults.GetDirectoryBucket(bundle.BundleName);
            if (!bucketBundles.TryGetValue(bucket, out List<CustomAssetBundleInfo> list))
            {
                list = new List<CustomAssetBundleInfo>();
                bucketBundles[bucket] = list;
            }

            list.Add(bundle);
        }

        var groups = new List<PackBuildGroup>();
        if (bootBundles.Count > 0)
        {
            groups.Add(new PackBuildGroup
            {
                PackNamePrefix = PackGroupingDefaults.BootPackName,
                Bundles = bootBundles,
            });
        }

        foreach (KeyValuePair<string, List<CustomAssetBundleInfo>> pair in bucketBundles)
        {
            string safeBucket = pair.Key.Replace('/', '_');
            groups.Add(new PackBuildGroup
            {
                PackNamePrefix = "pack_" + safeBucket,
                Bundles = pair.Value,
            });
        }

        for (int i = 0; i < standaloneBundles.Count; i++)
        {
            CustomAssetBundleInfo bundle = standaloneBundles[i];
            string safeName = bundle.BundleName.Replace('/', '_');
            groups.Add(new PackBuildGroup
            {
                PackNamePrefix = "pack_single_" + safeName,
                Bundles = new List<CustomAssetBundleInfo> { bundle },
            });
        }

        return groups;
    }

    /// <summary>
    /// First-Fit Decreasing：大包优先放入第一个放得下的 bin；
    /// 再把过小的相邻 bin 合并（不超过 MaxPackSize）。
    /// </summary>
    static List<List<CustomAssetBundleInfo>> PackBins(List<CustomAssetBundleInfo> bundles)
    {
        List<CustomAssetBundleInfo> sorted = bundles
            .OrderByDescending(b => b.Size)
            .ToList();

        var bins = new List<PackBin>();
        for (int i = 0; i < sorted.Count; i++)
        {
            CustomAssetBundleInfo bundle = sorted[i];
            PackBin targetBin = null;
            for (int j = 0; j < bins.Count; j++)
            {
                PackBin bin = bins[j];
                if (bin.TotalSize + bundle.Size <= PackGroupingDefaults.MaxPackSizeBytes)
                {
                    targetBin = bin;
                    break;
                }
            }

            if (targetBin == null)
            {
                targetBin = new PackBin();
                bins.Add(targetBin);
            }

            targetBin.Bundles.Add(bundle);
            targetBin.TotalSize += bundle.Size;
        }

        MergeSmallBins(bins);

        var result = new List<List<CustomAssetBundleInfo>>();
        for (int i = 0; i < bins.Count; i++)
        {
            result.Add(bins[i].Bundles);
        }

        return result;
    }

    static void MergeSmallBins(List<PackBin> bins)
    {
        if (bins.Count <= 1)
        {
            return;
        }

        for (int i = bins.Count - 1; i >= 1; i--)
        {
            PackBin current = bins[i];
            if (current.TotalSize >= PackGroupingDefaults.MinPackSizeBytes)
            {
                continue;
            }

            PackBin previous = bins[i - 1];
            if (previous.TotalSize + current.TotalSize <= PackGroupingDefaults.MaxPackSizeBytes)
            {
                previous.Bundles.AddRange(current.Bundles);
                previous.TotalSize += current.TotalSize;
                bins.RemoveAt(i);
            }
        }
    }

    /// <summary>ZIP entry 名使用 BundleName（正斜杠），与运行时解压路径一致。</summary>
    static void CreatePackZip(string zipPath, string platformBundlePath, List<CustomAssetBundleInfo> bundles)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using (FileStream stream = File.Create(zipPath))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            for (int i = 0; i < bundles.Count; i++)
            {
                CustomAssetBundleInfo bundle = bundles[i];
                string sourcePath = ResolveBundleFilePath(platformBundlePath, bundle.BundleName);
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("找不到 AB 文件: " + sourcePath);
                }

                ZipArchiveEntry entry = archive.CreateEntry(bundle.BundleName.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                using (Stream entryStream = entry.Open())
                using (FileStream sourceStream = File.OpenRead(sourcePath))
                {
                    sourceStream.CopyTo(entryStream);
                }
            }
        }
    }

    static string ResolveBundleFilePath(string platformBundlePath, string bundleName)
    {
        string normalized = bundleName.ToLowerInvariant().Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string fullPath = platformBundlePath;
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

    class PackBuildGroup
    {
        public string PackNamePrefix;
        public List<CustomAssetBundleInfo> Bundles;
    }

    class PackBin
    {
        public readonly List<CustomAssetBundleInfo> Bundles = new List<CustomAssetBundleInfo>();
        public long TotalSize;
    }
}
