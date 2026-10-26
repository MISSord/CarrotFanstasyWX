using System.IO;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 扫描落盘 AB，构建清单数据（Size/MD5/扁平依赖），不写文件、不合并 Pack、不签名。
    /// <para>后续由 <see cref="MergePacksStep"/>、<see cref="WriteManifestJsonStep"/> 等继续处理。</para>
    /// </summary>
    public sealed class GenerateManifestDataStep : IBuildStep
    {
        public string Name
        {
            get { return "GenerateManifestData"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!Directory.Exists(ctx.PlatformPath))
            {
                return BuildStepResult.Failed("AB 输出目录不存在：" + ctx.PlatformPath);
            }

            AssetBundlePackager.ManifestBuildData data = AssetBundlePackager.BuildManifestData(
                ctx.PlatformPath,
                ctx.Request.BuildTarget,
                ctx.Request.ManifestVersion,
                (int)ctx.Request.Compression);

            if (data == null || data.Manifest == null)
            {
                return BuildStepResult.Failed("清单数据构建失败");
            }

            ctx.Set(data);
            ctx.Result.Manifest = data.Manifest;

            return BuildStepResult.Success(
                data.Manifest.AssetBundles.Count + " 个包，登记未落盘 " + data.MissingOnDisk.Count + " 个");
        }
    }
}