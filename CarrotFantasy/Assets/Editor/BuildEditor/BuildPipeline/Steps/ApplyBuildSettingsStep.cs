namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>把本次请求参数写回 EditorPrefs（构建窗口下次打开时沿用）。</summary>
    public sealed class ApplyBuildSettingsStep : IBuildStep
    {
        public string Name
        {
            get { return "ApplyBuildSettings"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            AssetBundleBuildSettings.SetOutputRoot(ctx.Request.OutputRoot);
            AssetBundleBuildSettings.SetBuildTarget(ctx.Request.BuildTarget);
            AssetBundleBuildSettings.SetCompressionType(ctx.Request.Compression);
            return BuildStepResult.Success(ctx.Request.BuildTarget.ToString());
        }
    }
}