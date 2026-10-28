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
        };
    }

    private const string UseStepPipelinePrefKey = "AB_UseStepPipeline";

    /// <summary>
    /// 是否用步骤执行器跑 <see cref="BuildAndManifest"/>（默认关闭）。
    /// <para>
    /// 命令行 <c>abBuild</c> 不读这个开关，固定走步骤表。Profile 用资产上自己的开关。
    /// 菜单 <c>Tools/AssetBundle/构建流程</c> 只影响 <see cref="BuildAndManifest"/>。
    /// </para>
    /// </summary>
    public static bool UseStepPipeline
    {
        get { return EditorPrefs.GetBool(UseStepPipelinePrefKey, false); }
        set { EditorPrefs.SetBool(UseStepPipelinePrefKey, value); }
    }

    /// <summary>
    /// 按 <see cref="UseStepPipeline"/> 走步骤执行器或旧流程。
    /// 命令行打包请用 <see cref="BuildAndManifestWithSteps"/>。
    /// </summary>
    public static BuildResult BuildAndManifest(BuildRequest request)
    {
        return UseStepPipeline
            ? BuildAndManifestWithSteps(request)
            : RunLegacyFlow(request);
    }

    /// <summary>
    /// 用步骤执行器跑完整流程。
    /// <para>
    /// 步骤列表见 <see cref="AbBuildStepCatalog.Desktop"/>：校验 → 图集/Lua → AB →
    /// 清单数据 → Pack 合并 → JSON → 签名 → version → 拷贝/配置 → 收尾。
    /// 旧的整体流程保留在 <see cref="RunLegacyFlow"/>，可通过
    /// <see cref="UseStepPipeline"/> 与步骤版做 A/B 对比。
    /// </para>
    /// </summary>
    public static BuildResult BuildAndManifestWithSteps(
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

    /// <summary>改造前的整体流程（保持原样，作为基准实现与回退路径）。</summary>
    internal static BuildResult RunLegacyFlow(BuildRequest request)
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

        // —— 步骤 1.5：Lua 脚本包 ——
        // 必须早于 AB 打包：lua_bundle.bytes 是 AB 的一个资产，漏打的话包里是旧脚本，
        // 症状是「改了 Lua 没生效」，比打包失败难查得多，所以失败即中止整次打包。
        if (!CarrotFantasy.Editor.LuaBundleBuilder.Build(out string luaSummary))
        {
            Debug.LogError("[AB Build] Lua Bundle 打包失败，已中止：\n" + luaSummary);
            return result;
        }

        Debug.Log("[AB Build] " + luaSummary.Replace('\n', ' '));

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
            copyToStreamingAssets: false,
            backend: request.Backend,
            sbpResults: out _);

        if (!built)
        {
            return result;
        }

        // —— 步骤 3+4：清单 + Pack 合并——
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
        if (request.CopyToStreamingAssets
            && !AssetBundlePackager.CopyToStreamingAssets(platformPath, request.BuildTarget))
        {
            return result;
        }

        if (!AssetBundleBuildSettings.WriteRuntimeConfig(request.BuildTarget, request.RuntimeEnv))
        {
            Debug.LogError("[AB Build] 写入运行配置失败，已中止。");
            return result;
        }

        result.Success = true;
        return result;
    }
}

/// <summary>打包流程相关菜单（步骤执行器开关）。</summary>
public static class AssetBundleBuildPipelineMenu
{
    private const string TogglePath = "Tools/AssetBundle/构建流程/使用步骤执行器（实验）";

    [MenuItem(TogglePath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(TogglePath, AssetBundleBuildPipeline.UseStepPipeline);
        return true;
    }

    [MenuItem(TogglePath, false, 41)]
    private static void Toggle()
    {
        AssetBundleBuildPipeline.UseStepPipeline = !AssetBundleBuildPipeline.UseStepPipeline;
        Debug.Log("[AB Build] 步骤执行器已"
                  + (AssetBundleBuildPipeline.UseStepPipeline ? "启用（下次打包走步骤列表）" : "关闭（回到旧的整体流程）"));
    }
}
