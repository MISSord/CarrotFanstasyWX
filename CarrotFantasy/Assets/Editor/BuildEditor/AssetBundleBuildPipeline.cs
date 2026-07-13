using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AB 打包统一入口（Editor）。
///
/// 完整流程：
/// 1. 可选：打包 UI 图集（AtlasPackager）
/// 2. BuildPipeline 生成平台 AB 到 Build/AssetBundles/{平台}/
/// 3. 扫描落盘 AB，生成 custom_manifest.json（含 Size/MD5/依赖）
/// 4. AssetBundlePackMerger 按规则合并 ZIP Pack，写入 DownloadPacks
/// 5. 可选：拷贝到 StreamingAssets；写入 ab_runtime_config.json（CDN 模板）
///
/// 运行时热更会读取该清单：先按 AB 对比差异，再按 DownloadPacks 合并下载。
/// </summary>
public static class AssetBundleBuildPipeline
{
    public struct BuildRequest
    {
        public string OutputRoot;
        public BuildTarget BuildTarget;
        /// <summary>写入清单 CompressedFormat：0=LZMA，1=LZ4(ChunkBased)，2=无压缩。</summary>
        public CompressionType Compression;
        public int ManifestVersion;
        public bool ClearOutputFolder;
        public bool CopyToStreamingAssets;
        /// <summary>强制重建时必须与 Compression 做按位或，否则会丢掉压缩选项、默认为 LZMA。</summary>
        public bool ForceRebuild;
        public bool ShowManifestDialog;
        public bool PackAtlasesBeforeBuild;
    }

    public struct BuildResult
    {
        public bool Success;
        public CustomManifest Manifest;
        public string PlatformBundlePath;
    }

    public static BuildRequest CreateDefaultRequest()
    {
        BuildTarget target = AssetBundleBuildSettings.GetBuildTarget();
        return new BuildRequest
        {
            OutputRoot = AssetBundleBuildSettings.GetOutputRoot(),
            BuildTarget = target,
            Compression = AssetBundleBuildSettings.GetCompressionType(),
            ManifestVersion = AssetBundleBuildSettings.SuggestNextManifestVersion(target),
            ClearOutputFolder = true,
            CopyToStreamingAssets = false,
            ForceRebuild = false,
            ShowManifestDialog = true,
            PackAtlasesBeforeBuild = true,
        };
    }

    public static BuildResult BuildAndManifest(BuildRequest request)
    {
        var result = new BuildResult();
        if (string.IsNullOrEmpty(request.OutputRoot))
        {
            Debug.LogError("[AB Build] 输出根目录为空。");
            return result;
        }

        AssetBundleBuildSettings.SetOutputRoot(request.OutputRoot);
        AssetBundleBuildSettings.SetBuildTarget(request.BuildTarget);
        AssetBundleBuildSettings.SetCompressionType(request.Compression);

        // 平台输出目录：{OutputRoot}/{StandaloneWindows|Android|...}
        // 注意：Win32/Win64 共用 StandaloneWindows，交替打包会互相覆盖。
        string platformPath = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", request.OutputRoot)),
            AssetBundlePackager.GetPlatformFolder(request.BuildTarget));
        result.PlatformBundlePath = platformPath;

        // —— 步骤 1：图集 ——
        if (request.PackAtlasesBeforeBuild)
        {
            AtlasPackager.PackForAbBuild();
        }

        // —— 步骤 2：打 AB ——
        // ForceRebuild 只能附加，不能替换压缩选项。
        BuildAssetBundleOptions options = AssetBundlePackager.GetCompressionOption(request.Compression);
        if (request.ForceRebuild)
        {
            options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
        }

        bool built = AssetBundlePackager.BuildAssetBundles(
            platformPath,
            request.BuildTarget,
            options,
            request.ClearOutputFolder,
            copyToStreamingAssets: false);

        if (!built)
        {
            return result;
        }

        // —— 步骤 3+4：清单 + Pack 合并（GenerateManifest 会先同步 HybridCLR DLL）——
        result.Manifest = AssetBundlePackager.GenerateManifest(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", request.OutputRoot)),
            request.BuildTarget,
            request.ManifestVersion,
            (int)request.Compression,
            request.ShowManifestDialog);

        if (result.Manifest == null)
        {
            return result;
        }

        // —— 步骤 5：可选内置包 + 运行时 CDN 配置 ——
        if (request.CopyToStreamingAssets)
        {
            AssetBundlePackager.CopyToStreamingAssets(platformPath, request.BuildTarget);
        }

        AssetBundleBuildSettings.WriteRuntimeConfig(request.BuildTarget);
        result.Success = true;
        return result;
    }
}
