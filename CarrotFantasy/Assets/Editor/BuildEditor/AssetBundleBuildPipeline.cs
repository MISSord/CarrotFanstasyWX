using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>AB 打包统一入口：Build → Manifest → 可选发布到 StreamingAssets。</summary>
public static class AssetBundleBuildPipeline
{
    public struct BuildRequest
    {
        public string OutputRoot;
        public BuildTarget BuildTarget;
        public CompressionType Compression;
        public int ManifestVersion;
        public bool ClearOutputFolder;
        public bool CopyToStreamingAssets;
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

        string platformPath = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", request.OutputRoot)),
            AssetBundlePackager.GetPlatformFolder(request.BuildTarget));
        result.PlatformBundlePath = platformPath;

        if (request.PackAtlasesBeforeBuild)
        {
            AtlasPackager.PackForAbBuild();
        }

        BuildAssetBundleOptions options = request.ForceRebuild
            ? BuildAssetBundleOptions.ForceRebuildAssetBundle
            : AssetBundlePackager.GetCompressionOption(request.Compression);

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

        if (request.CopyToStreamingAssets)
        {
            AssetBundlePackager.CopyToStreamingAssets(platformPath, request.BuildTarget);
        }

        AssetBundleBuildSettings.WriteRuntimeConfig(request.BuildTarget);
        result.Success = true;
        return result;
    }
}
