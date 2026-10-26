namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>写入运行时 CDN 配置（StreamingAssets/ab_runtime_config.json）。</summary>
    public sealed class WriteRuntimeConfigStep : IBuildStep
    {
        public string Name
        {
            get { return "WriteRuntimeConfig"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            AssetBundleBuildSettings.WriteRuntimeConfig(ctx.Request.BuildTarget);
            return BuildStepResult.Success();
        }
    }
}