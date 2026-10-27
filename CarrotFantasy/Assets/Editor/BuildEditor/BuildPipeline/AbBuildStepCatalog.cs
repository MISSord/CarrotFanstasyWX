using System.Collections.Generic;
using UnityEditor;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 打包流程的唯一事实来源：**显式列出步骤、顺序、是否关键**。
    /// <para>
    /// 每一行 = <c>BuildStep.Required/Optional("名字", AbBuildSteps.方法)</c>：
    /// 名字进日志，方法体在 <see cref="AbBuildSteps"/>，顺序就是数组顺序。
    /// 加步骤/调顺序/改关键性都只改这一处，review 时一目了然。
    /// </para>
    /// <para>
    /// 平台差异用"不同数组"表达（见 <see cref="ForTarget"/>），不在步骤内部写平台分支。
    /// </para>
    /// </summary>
    public static class AbBuildStepCatalog
    {
        /// <summary>桌面平台（Windows / macOS）的 AB 打包流程。</summary>
        private static readonly BuildStep[] DesktopSteps =
        {
            // 1) 校验与设置
            BuildStep.Required("ValidateRequest", AbBuildSteps.ValidateRequest),
            BuildStep.Required("ApplyBuildSettings", AbBuildSteps.ApplyBuildSettings),

            // 2) 资源预处理（必须先于 AB）
            BuildStep.Optional("PackAtlas", AbBuildSteps.PackAtlas),
            BuildStep.Required("BuildLuaBundle", AbBuildSteps.BuildLuaBundle),

            // 3) 打 AB
            BuildStep.Required("CleanOutput", AbBuildSteps.CleanOutput),
            BuildStep.Required("BuildBundles", AbBuildSteps.BuildBundles),

            // 4) 清单与分发包
            BuildStep.Required("GenerateManifestData", AbBuildSteps.GenerateManifestData),
            BuildStep.Required("MergePacks", AbBuildSteps.MergePacks),
            BuildStep.Required("WriteManifestJson", AbBuildSteps.WriteManifestJson),
            BuildStep.Required("SignManifest", AbBuildSteps.SignManifest),
            BuildStep.Required("WriteVersion", AbBuildSteps.WriteVersion),

            // 5) 交付
            BuildStep.Optional("RefreshAssets", AbBuildSteps.RefreshAssets),
            BuildStep.Optional("CopyToStreamingAssets", AbBuildSteps.CopyToStreamingAssets),
            BuildStep.Required("WriteRuntimeConfig", AbBuildSteps.WriteRuntimeConfig),

            // 6) 可选：上传到云服务器（Profile 注入配置；未配置则跳过）
            BuildStep.Optional("UploadToServer", AbBuildSteps.UploadToServer),

            // 7) 收尾
            BuildStep.Optional("ShowManifestDialog", AbBuildSteps.ShowManifestDialog),
            BuildStep.Required("FinalizeResult", AbBuildSteps.FinalizeResult),
        };

        /// <summary>桌面平台步骤列表（静态只读，零分配）。</summary>
        public static IReadOnlyList<BuildStep> Desktop()
        {
            return DesktopSteps;
        }

        /// <summary>
        /// 按目标平台选择步骤列表。
        /// <para>
        /// 将来 Android 需要在这里返回自己的数组（插入签名/Manifest 补丁步骤、
        /// 替换 StreamingAssets 策略），而不是在现有步骤里写平台分支。
        /// </para>
        /// </summary>
        public static IReadOnlyList<BuildStep> ForTarget(BuildTarget target)
        {
            switch (target)
            {
                default:
                    return DesktopSteps;
            }
        }
    }
}
