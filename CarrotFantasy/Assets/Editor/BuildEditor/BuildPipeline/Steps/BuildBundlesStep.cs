using UnityEditor;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 构建 AB（内部按 <see cref="AssetBundlePackager.UseScriptableBuildPipeline"/> 选 SBP / built-in）。
    /// <para>
    /// 清目录已由 <see cref="CleanOutputStep"/> 负责，这里固定 <c>clearFolders:false</c>，
    /// 拷 StreamingAssets 也由后续独立步骤负责。
    /// </para>
    /// </summary>
    public sealed class BuildBundlesStep : IBuildStep
    {
        public string Name
        {
            get { return "BuildBundles"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            // ForceRebuild 只能附加，不能替换压缩选项。
            BuildAssetBundleOptions options = AssetBundlePackager.GetCompressionOption(ctx.Request.Compression);
            if (ctx.Request.ForceRebuild)
            {
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            }

            bool built = AssetBundlePackager.BuildAssetBundles(
                ctx.PlatformPath,
                ctx.Request.BuildTarget,
                options,
                clearFolders: false,
                copyToStreamingAssets: false);

            return built
                ? BuildStepResult.Success(AssetBundlePackager.UseScriptableBuildPipeline ? "SBP" : "built-in")
                : BuildStepResult.Failed("AB 构建失败（详见上方日志）");
        }
    }
}