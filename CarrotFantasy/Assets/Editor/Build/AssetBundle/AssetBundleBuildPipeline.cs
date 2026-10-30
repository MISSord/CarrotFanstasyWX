using System.IO;
using CarrotFantasy.Editor.BuildPipeline;
using UnityEditor;
using UnityEngine;

/// <summary>AB 构建后端。不同 Profile 可以选不同后端。</summary>
public enum AbBuildBackend
{
    /// <summary>Scriptable Build Pipeline（默认，支持代码化布局与 BuildCache）。</summary>
    ScriptableBuildPipeline = 0,

    /// <summary>built-in BuildPipeline.BuildAssetBundles（回退对比用）。</summary>
    BuiltIn = 1,
}

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
        /// <summary>写入 ab_runtime_config 的环境。空则保留已有 env，没有文件时用 dev。</summary>
        public string RuntimeEnv;

        /// <summary>构建后端；Profile 可独立选择，命令行默认沿用全局开关。</summary>
        public AbBuildBackend Backend;

        /// <summary>包布局：Meta（.meta 包名）或 Code（阶段 C 的代码规则）。</summary>
        public AbBundleLayoutMode Layout;
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
            Backend = AssetBundlePackager.UseScriptableBuildPipeline
                ? AbBuildBackend.ScriptableBuildPipeline
                : AbBuildBackend.BuiltIn,
            Layout = AbBundleLayoutMode.Meta,
        };
    }

    /// <summary>
    /// 用步骤表跑完整流程。
    /// <para>
    /// 步骤列表见 <see cref="AbBuildStepCatalog.Desktop"/>：校验 → 图集/Lua → AB →
    /// 清单数据 → Pack 合并 → JSON → 签名 → version → 拷贝/配置 → 收尾。
    /// </para>
    /// </summary>
    public static BuildResult BuildAndManifest(
        BuildRequest request,
        BuildExecutionOptions options = null,
        System.Action<BuildContext> configureContext = null)
    {
        var ctx = new BuildContext { Request = request };
        ctx.OutputRootFullPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", request.OutputRoot));
        ctx.PlatformPath = Path.Combine(
            ctx.OutputRootFullPath,
            AssetBundlePackager.GetPlatformFolder(request.BuildTarget));

        // 调用方可以在这里注入额外上下文（例如 Profile 的 SFTP 上传配置），
        // 步骤通过 ctx.Get<T>() 读取；不注入时对应步骤自动跳过。
        configureContext?.Invoke(ctx);

        BuildReport report = BuildExecutor.Execute(
            ctx,
            AbBuildStepCatalog.ForTarget(request.BuildTarget),
            options);

        BuildResult result = ctx.Result;
        result.Success = report.Success && result.Success;
        result.PlatformBundlePath = ctx.PlatformPath;
        return result;
    }
}
