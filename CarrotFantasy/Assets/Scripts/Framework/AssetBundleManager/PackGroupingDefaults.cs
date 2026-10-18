/// <summary>
/// Pack 合并下载的默认分组参数（目录分桶 + 大小装箱）。
/// Editor（PackMerger）与 Runtime（Planner）共用，改阈值两边一起生效。
///
/// 尺寸约定：
/// - MaxPackSizeBytes：单 ZIP 上限（装箱硬约束）
/// - MinPackSizeBytes：过小 bin 尝试与相邻合并的阈值
/// - StandaloneThresholdBytes：超过则单独成 Pack，避免大图集拖累小更新
/// </summary>
public static class PackGroupingDefaults
{
    public const long TargetPackSizeBytes = 8L * 1024 * 1024;
    public const long MaxPackSizeBytes = 15L * 1024 * 1024;
    public const long MinPackSizeBytes = 1L * 1024 * 1024;
    public const long StandaloneThresholdBytes = 5L * 1024 * 1024;

    public const string PacksFolderName = "packs";
    public const string PackFileExtension = ".zip";
    public const string BootPackName = "pack_boot";

    /// <summary>启动阶段优先需要的 AB；改名后需同步更新，否则会落入普通目录桶。</summary>
    public static readonly string[] BootBundles =
    {
        "ui/view/login_prefab",
        "ui/view/loadingview_prefab",
        "ui/view_prefab",
    };

    /// <summary>名称包含这些片段的包强制独立 Pack（如大图集）。</summary>
    public static readonly string[] StandalonePatterns =
    {
        "images_atlas",
    };

    public static bool IsBootBundle(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            return false;
        }

        string normalized = bundleName.ToLowerInvariant().Replace('\\', '/');
        for (int i = 0; i < BootBundles.Length; i++)
        {
            if (normalized == BootBundles[i])
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldStandalone(CustomAssetBundleInfo bundle)
    {
        if (bundle == null)
        {
            return false;
        }

        if (bundle.Size >= StandaloneThresholdBytes)
        {
            return true;
        }

        string normalized = bundle.BundleName.ToLowerInvariant().Replace('\\', '/');
        for (int i = 0; i < StandalonePatterns.Length; i++)
        {
            if (normalized.Contains(StandalonePatterns[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>取 BundleName 前两级目录作为分桶键，如 ui/view/mainview_prefab → ui/view。</summary>
    public static string GetDirectoryBucket(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            return "other";
        }

        string normalized = bundleName.ToLowerInvariant().Replace('\\', '/');
        string[] parts = normalized.Split('/');
        if (parts.Length >= 2)
        {
            return parts[0] + "/" + parts[1];
        }

        if (parts.Length == 1)
        {
            return parts[0];
        }

        return "other";
    }
}
