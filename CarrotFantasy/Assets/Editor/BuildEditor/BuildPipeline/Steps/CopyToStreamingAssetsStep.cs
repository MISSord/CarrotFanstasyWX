namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>把平台 AB 目录拷进 StreamingAssets（受 Request.CopyToStreamingAssets 控制，可选）。</summary>
    public sealed class CopyToStreamingAssetsStep : IBuildStep
    {
        public string Name
        {
            get { return "CopyToStreamingAssets"; }
        }

        public bool Required
        {
            get { return false; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!ctx.Request.CopyToStreamingAssets)
            {
                return BuildStepResult.Skipped("CopyToStreamingAssets=false");
            }

            AssetBundlePackager.CopyToStreamingAssets(ctx.PlatformPath, ctx.Request.BuildTarget);
            return BuildStepResult.Success();
        }
    }
}