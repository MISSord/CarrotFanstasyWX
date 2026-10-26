using System.IO;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 清单签名。开发宏 <c>CF_DEV_SKIP_MANIFEST_SIGN</c> 生效时只告警不签名，
    /// 此时 <see cref="ManifestSigner.TrySign"/> 仍返回 true（与改造前一致）。
    /// </summary>
    public sealed class SignManifestStep : IBuildStep
    {
        public string Name
        {
            get { return "SignManifest"; }
        }

        public bool Required
        {
            get { return true; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            string manifestPath = Path.Combine(ctx.PlatformPath, "custom_manifest.json");
            if (!File.Exists(manifestPath))
            {
                return BuildStepResult.Failed("清单文件不存在：" + manifestPath);
            }

            if (!ManifestSigner.TrySign(manifestPath, out string error))
            {
                return BuildStepResult.Failed(error);
            }

            return BuildStepResult.Success();
        }
    }
}