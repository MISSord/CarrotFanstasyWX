using System.Collections.Generic;
using UnityEditor;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>
    /// 打包流程的唯一事实来源：**显式列出步骤及其顺序**。
    /// <para>
    /// 为什么不用反射自动发现：
    /// </para>
    /// <list type="bullet">
    ///   <item>顺序必须一眼可见——这个文件就是流程文档，review 时能看到顺序变更；</item>
    ///   <item>漏注册/改名会直接编译报错，而不是"某个步骤悄悄不执行"；</item>
    ///   <item>步骤可以带构造参数（例如 <c>new SignManifestStep(requireSignature: true)</c>）；</item>
    ///   <item>平台差异用不同列表表达，不需要在步骤内部散落 <c>if (target == ...)</c>。</item>
    /// </list>
    /// <para>SBP 的 <c>DefaultBuildTasks</c> 也是同样的做法（显式 new 出 List&lt;IBuildTask&gt;）。</para>
    /// </summary>
    public static class AbBuildStepCatalog
    {
        /// <summary>
        /// 桌面平台（Windows / macOS）的 AB 打包流程。
        /// <para>
        /// 顺序即依赖：图集/Lua 先于 AB；清单数据先于 Pack 合并与 JSON 写入；
        /// 写 JSON 之后才能签名；签名之后才写 version.txt。
        /// </para>
        /// </summary>
        public static IReadOnlyList<IBuildStep> Desktop()
        {
            return new IBuildStep[]
            {
                // 1) 校验与设置
                new ValidateRequestStep(),
                new ApplyBuildSettingsStep(),

                // 2) 资源预处理（必须在打 AB 之前）
                new PackAtlasStep(),        // 可关：request.PackAtlasesBeforeBuild
                new BuildLuaBundleStep(),   // 关键：lua_bundle.bytes 是 AB 的资产

                // 3) 打 AB
                new CleanOutputStep(),      // 可关：request.ClearOutputFolder
                new BuildBundlesStep(),     // 内部按 UseScriptableBuildPipeline 选 SBP / built-in

                // 4) 清单与分发包
                new GenerateManifestDataStep(),  // 扫描落盘 → Size/MD5/依赖
                new MergePacksStep(),            // packs/*.zip → DownloadPacks
                new WriteManifestJsonStep(),     // custom_manifest.json
                new SignManifestStep(),          // 签名（dev 宏生效时只告警）
                new WriteVersionStep(),          // version.txt

                // 5) 交付
                new RefreshAssetsStep(),
                new CopyToStreamingAssetsStep(), // 可关：request.CopyToStreamingAssets
                new WriteRuntimeConfigStep(),    // ab_runtime_config.json

                // 6) 收尾
                new ShowManifestDialogStep(),  // 可关：request.ShowManifestDialog（批处理自动跳过）
                new FinalizeResultStep(),
            };
        }

        /// <summary>
        /// 按目标平台选择步骤列表。
        /// <para>
        /// 目前只支持桌面（PC / macOS）；将来 Android 需要在这里返回自己的列表，
        /// 例如插入 <c>new PatchAndroidManifestStep()</c>、替换 StreamingAssets 策略，
        /// 而不是在现有步骤里写平台分支。
        /// </para>
        /// </summary>
        public static IReadOnlyList<IBuildStep> ForTarget(BuildTarget target)
        {
            switch (target)
            {
                default:
                    return Desktop();
            }
        }
    }
}
