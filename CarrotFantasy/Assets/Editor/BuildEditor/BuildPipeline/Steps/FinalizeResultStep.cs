namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>收尾：把上下文里的产物落到最终结果，供调用方（窗口 / 批处理）读取。</summary>
    public sealed class FinalizeResultStep : IBuildStep
    {
        public string Name
        {
            get { return "FinalizeResult"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (ctx.Result.Manifest == null)
            {
                return BuildStepResult.Failed("清单为空，不能标记为成功");
            }

            ctx.Result.Success = true;
            ctx.Result.PlatformBundlePath = ctx.PlatformPath;
            return BuildStepResult.Success(
                ctx.Result.Manifest.AssetBundles.Count + " 个包 → " + ctx.PlatformPath);
        }
    }
}