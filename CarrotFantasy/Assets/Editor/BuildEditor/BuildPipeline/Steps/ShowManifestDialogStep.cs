using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 打包完成后弹"成功"对话框（受 <c>Request.ShowManifestDialog</c> 控制）。
    /// <para>
    /// 从 <see cref="AssetBundlePackager.GenerateManifest"/> 里拆出来，
    /// 让"弹窗"不再埋在清单生成逻辑里；批处理模式下自动跳过，避免阻塞。
    /// </para>
    /// </summary>
    public sealed class ShowManifestDialogStep : IBuildStep
    {
        public string Name
        {
            get { return "ShowManifestDialog"; }
        }

        public bool Required
        {
            get { return false; }
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            if (!ctx.Request.ShowManifestDialog)
            {
                return BuildStepResult.Skipped("ShowManifestDialog=false");
            }

            if (Application.isBatchMode)
            {
                return BuildStepResult.Skipped("BatchMode");
            }

            AssetBundlePackager.ManifestBuildData data = ctx.Get<AssetBundlePackager.ManifestBuildData>();
            int bundleCount = data != null && data.Manifest != null ? data.Manifest.AssetBundles.Count : 0;
            int missingCount = data != null && data.MissingOnDisk != null ? data.MissingOnDisk.Count : 0;

            EditorUtility.DisplayDialog(
                "成功",
                string.Format(
                    "清单已生成：{0} 个 AB 包（以输出目录实际文件为准）。\n" +
                    "有资源登记但未构建出文件：{1} 个（详见 Console，多为空文件夹包名或需重新打 AB）。",
                    bundleCount,
                    missingCount),
                "确定");

            return BuildStepResult.Success();
        }
    }
}