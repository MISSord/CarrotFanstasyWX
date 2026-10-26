namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 打 AB 前先刷新图集（可选）。
    /// <para>非关键步骤：图集失败只告警，继续打 AB（与改造前 if 分支的语义一致）。</para>
    /// </summary>
    public sealed class PackAtlasStep : IBuildStep
    {
        public string Name
        {
            get { return "PackAtlas"; }
        }

        public bool Required
        {
            get { return false; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!ctx.Request.PackAtlasesBeforeBuild)
            {
                return BuildStepResult.Skipped("PackAtlasesBeforeBuild=false");
            }

            AtlasPackager.PackForAbBuild();
            return BuildStepResult.Success();
        }
    }
}