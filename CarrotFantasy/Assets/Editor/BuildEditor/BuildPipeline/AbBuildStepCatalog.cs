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
        /// 当前桌面平台（Windows / macOS）的流程。
        /// <para>
        /// 阶段 1：整个旧流程先作为一个步骤接入，行为与改造前完全一致；
        /// 后续按下面的"目标清单"逐步拆开，每拆一步都要对比产物。
        /// </para>
        /// </summary>
        public static IReadOnlyList<IBuildStep> Desktop()
        {
            return new IBuildStep[]
            {
                new LegacyPipelineStep(),
            };
        }

        /// <summary>
        /// 目标形态（阶段 2 之后逐步替换上面的列表）：
        /// <code>
        /// new ValidateRequestStep(),          // 路径/平台/参数
        /// new ApplyBuildSettingsStep(),       // 写入 EditorPrefs / BuildSettings
        /// new PackAtlasStep(),                // AtlasPackager（可关）
        /// new BuildLuaBundleStep(),           // LuaBundleBuilder（关键）
        /// new RefreshAssetsStep(),            // AssetDatabase.Refresh（显式化）
        /// new CleanOutputStep(),              // 清输出目录（可关）
        /// new BuildBundlesStep(),             // SBP / built-in
        /// new NormalizeBundleNamesStep(),     // 文件名小写
        /// new AuditBundleLayoutStep(),        // 依赖/体积审计（新增）
        /// new GenerateManifestStep(),         // custom_manifest.json
        /// new MergePacksStep(),               // packs/*.zip
        /// new SignManifestStep(),             // 签名（prod 必需）
        /// new WriteVersionStep(),             // version.txt
        /// new WriteRuntimeConfigStep(),       // ab_runtime_config.json
        /// new CopyToStreamingAssetsStep(),    // 可选
        /// new ReportStep(),                   // 汇总表
        /// </code>
        /// </summary>
        public static IReadOnlyList<IBuildStep> ForTarget(BuildTarget target)
        {
            // 目前只支持桌面（PC / macOS）；Android/iOS/WebGL 的差异列表在这里分支。
            switch (target)
            {
                default:
                    return Desktop();
            }
        }
    }
}
