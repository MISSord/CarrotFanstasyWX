namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>写 version.txt（内容为清单里的 BuildTime，与服务端 version.txt 对应）。</summary>
    public sealed class WriteVersionStep : IBuildStep
    {
        public string Name
        {
            get { return "WriteVersion"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("缺少清单数据");
            }

            AssetBundlePackager.WriteVersionFile(data.Manifest, ctx.PlatformPath);
            return BuildStepResult.Success(data.Manifest.BuildTime);
        }
    }
}