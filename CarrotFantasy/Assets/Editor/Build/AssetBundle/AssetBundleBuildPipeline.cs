using System.IO;
using CarrotFantasy.Editor.BuildPipeline;
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

    private const string UseStepPipelinePrefKey = "AB_UseStepPipeline";

    /// <summary>
    /// 是否用"步骤执行器"跑打包流程（阶段 1 默认关闭，行为与改造前一致）。
    /// <para>
    /// 打开后走 <see cref="BuildAndManifestWithSteps"/>：执行器按
    /// <see cref="AbBuildStepCatalog"/> 里的显式列表逐步执行。
    /// 迁移期用它做 A/B 对比，菜单 <c>Tools/AssetBundle/构建流程</c> 可切换。
    /// </para>
    /// </summary>
    public static bool UseStepPipeline
    {
        get { return EditorPrefs.GetBool(UseStepPipelinePrefKey, false); }
        set { EditorPrefs.SetBool(UseStepPipelinePrefKey, value); }
    }

    /// <summary>
    /// 统一入口：按开关决定走"步骤执行器"还是"旧的整体流程"。
    /// 调用方（编辑器窗口 / 批处理）不需要感知两者差异。
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
            copyToStreamingAssets: false);

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

        AssetBundleBuildSettings.WriteRuntimeConfig(request.BuildTarget);
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
