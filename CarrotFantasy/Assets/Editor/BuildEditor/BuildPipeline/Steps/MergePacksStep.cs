namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>把清单里的 AB 按规则合并成下载用 ZIP Pack，填充 DownloadPacks。</summary>
    public sealed class MergePacksStep : IBuildStep
    {
        public string Name
        {
            get { return "MergePacks"; }
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
                return BuildStepResult.Failed("缺少清单数据（检查 GenerateManifestData 是否在本步骤之前）");
            }

            AssetBundlePackMerger.BuildPacks(data.Manifest, ctx.PlatformPath);

            int packCount = data.Manifest.DownloadPacks != null ? data.Manifest.DownloadPacks.Count : 0;
            return BuildStepResult.Success(packCount + " 个 Pack");
        }
    }
}