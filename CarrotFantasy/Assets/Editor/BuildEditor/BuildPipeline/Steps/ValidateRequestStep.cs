namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>参数与平台校验：输出路径、目标平台是否支持同步 File IO 加载。</summary>
    public sealed class ValidateRequestStep : IBuildStep
    {
        public string Name
        {
            get { return "ValidateRequest"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.Request.OutputRoot))
            {
                return BuildStepResult.Failed("输出根目录为空（检查 AssetBundleBuildSettings / 构建窗口）");
            }

            if (!AssetBundlePackager.IsSupportedRuntimePlatform(ctx.Request.BuildTarget))
            {
                return BuildStepResult.Failed(
                    "当前 AB 运行时只支持 PC/macOS 的 File IO 加载，" + ctx.Request.BuildTarget
                    + " 需要先补 UnityWebRequestAssetBundle 分支");
            }

            if (string.IsNullOrEmpty(ctx.PlatformPath))
            {
                return BuildStepResult.Failed("平台输出目录未计算（检查 BuildAndManifestWithSteps）");
            }

            return BuildStepResult.Success(ctx.PlatformPath);
        }
    }
}