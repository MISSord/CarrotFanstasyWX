namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>写 custom_manifest.json（不做签名，签名是独立步骤）。</summary>
    public sealed class WriteManifestJsonStep : IBuildStep
    {
        public string Name
        {
            get { return "WriteManifestJson"; }
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

            if (!AssetBundlePackager.WriteManifestJson(data.Manifest, ctx.PlatformPath, out string error))
            {
                return BuildStepResult.Failed(error);
            }

            return BuildStepResult.Success("custom_manifest.json");
        }
    }
}