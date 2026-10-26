using System.Collections.Generic;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>步骤执行结果。</summary>
    public enum BuildStepStatus
    {
        Success = 0,

        /// <summary>本步骤判定不需要执行（如开关关闭、产物已是最新）。</summary>
        Skipped = 1,

        Failed = 2,
    }

    /// <summary>
    /// 单步结果。失败信息会进汇总表，便于出问题时一眼看到是哪一步。
    /// </summary>
    public sealed class BuildStepResult
    {
        public BuildStepStatus Status { get; private set; }

        public string Message { get; private set; }

        public bool IsSuccess
        {
            get { return this.Status != BuildStepStatus.Failed; }
        }

        public static BuildStepResult Success(string message = null)
        {
            return new BuildStepResult { Status = BuildStepStatus.Success, Message = message };
        }

        public static BuildStepResult Skipped(string message)
        {
            return new BuildStepResult { Status = BuildStepStatus.Skipped, Message = message };
        }

        public static BuildStepResult Failed(string message)
        {
            return new BuildStepResult { Status = BuildStepStatus.Failed, Message = message };
        }
    }

    /// <summary>
    /// 打包流程中的一个步骤。
    /// <para>
    /// 设计约定（与 SBP 的 <c>IBuildTask</c> 保持同一心智）：
    /// </para>
    /// <list type="bullet">
    ///   <item><b>顺序由调用方给出的列表决定</b>，步骤内部不关心自己排第几；</item>
    ///   <item>一个步骤只做一件事，超过 ~50 行就该再拆；</item>
    ///   <item>步骤之间只通过 <see cref="BuildContext"/> 交换数据，不互相直接调用；</item>
    ///   <item>步骤要可重入：失败重跑时不应把上一次的半成品当成有效产物。</item>
    /// </list>
    /// </summary>
    public interface IBuildStep
    {
        /// <summary>日志与报告里显示的名字，保持唯一。</summary>
        string Name { get; }

        /// <summary>
        /// 失败时是否中止整个流程。
        /// <para>
        /// true  = 关键步骤（Lua bundle、AB 构建、清单生成）；
        /// false = 可降级步骤（图集、拷贝 StreamingAssets、出 Player）。
        /// </para>
        /// </summary>
        bool Required { get; }

        BuildStepResult Run(BuildContext ctx);
    }

    /// <summary>单步执行记录（执行器填写，报告用）。</summary>
    public sealed class BuildStepRecord
    {
        public string Name;
        public BuildStepStatus Status;
        public string Message;
        public long ElapsedMilliseconds;
    }

    /// <summary>
    /// 执行选项：仅用于**调试**（只跑/跳过某些步骤）。
    /// <para>
    /// 正常打包不要用它——顺序与范围由步骤列表本身决定，这才是唯一事实来源。
    /// </para>
    /// </summary>
    public sealed class BuildExecutionOptions
    {
        /// <summary>只执行列表里的这些步骤（按列表顺序），null/空 = 全跑。</summary>
        public IReadOnlyCollection<string> Only;

        /// <summary>跳过这些步骤，null/空 = 不跳过。</summary>
        public IReadOnlyCollection<string> Skip;

        /// <summary>只打印将执行哪些步骤，不真正执行。</summary>
        public bool DryRun;
    }
}
