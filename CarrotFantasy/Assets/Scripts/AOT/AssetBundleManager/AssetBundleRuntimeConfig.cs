using System;

/// <summary>
/// 运行时 AB 热更配置，由 Editor 打包发布时写入 StreamingAssets/ab_runtime_config.json。
/// </summary>
[Serializable]
public class AssetBundleRuntimeConfig
{
    public const string FileName = "ab_runtime_config.json";

    /// <summary>环境标识：dev / staging / prod（与 BuildChannelDefines 常量一致）。</summary>
    public string env = "dev";

    /// <summary>远程清单根 URL 模板，{0} 为平台目录名（如 StandaloneWindows）。</summary>
    public string serverDownloadUrlTemplate = string.Empty;

    public string localSavePath = "DownloadedAssetBundles";
}
