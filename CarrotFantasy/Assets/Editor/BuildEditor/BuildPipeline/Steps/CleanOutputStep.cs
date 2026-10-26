namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 清空平台输出目录（受 <c>Request.ClearOutputFolder</c> 控制）。
    /// <para>
    /// 从 <see cref="AssetBundlePackager.BuildAssetBundles"/> 里拆出来，
    /// 这样 <see cref="BuildBundlesStep"/> 只负责"构建"，不再混"删目录"的副作用。
    /// </para>
    /// </summary>
    public sealed class CleanOutputStep : IBuildStep
    {
        public string Name
        {
            get { return "CleanOutput"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!ctx.Request.ClearOutputFolder)
            {
                return BuildStepResult.Skipped("ClearOutputFolder=false");
            }

            if (!AssetBundlePackager.TryCleanOutputDirectory(ctx.PlatformPath, out string error))
            {
                return BuildStepResult.Failed(error);
            }

            return BuildStepResult.Success(ctx.PlatformPath);
        }
    }
}