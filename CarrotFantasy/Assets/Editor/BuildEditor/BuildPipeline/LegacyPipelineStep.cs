namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 阶段 1 的过渡步骤：把改造前的整套流程（<see cref="AssetBundleBuildPipeline.RunLegacyFlow"/>）
    /// 原样包成一个步骤。
    /// <para>
    /// 目的：先让"执行器 + 步骤列表"这条链路跑起来，且**产物与改造前完全一致**；
    /// 之后每次只拆一个步骤出来（Lua bundle → AB → 清单 → Pack → 签名…），
    /// 每拆一步都对比产物 hash，避免一次性重构导致无法定位。
    /// </para>
    /// </summary>
    public sealed class LegacyPipelineStep : IBuildStep
    {
        public string Name
        {
            get { return "LegacyPipeline"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            AssetBundleBuildPipeline.BuildResult result = AssetBundleBuildPipeline.RunLegacyFlow(ctx.Request);
            ctx.Result = result;

            if (!result.Success)
            {
                // 具体原因旧流程已经打日志了，这里只负责把"失败"传给执行器。
                return BuildStepResult.Failed("旧流程返回失败（详见上方日志）");
            }

            return BuildStepResult.Success(
                result.Manifest != null
                    ? result.Manifest.AssetBundles.Count + " 个包"
                    : null);
        }
    }
}
