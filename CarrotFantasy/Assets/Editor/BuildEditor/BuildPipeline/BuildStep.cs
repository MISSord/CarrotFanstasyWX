using System.Collections.Generic;

namespace CarrotFantasy.Editor.BuildPipeline
{
    /// <summary>步骤执行结果。</summary>
    public enum BuildStepStatus
    {
        Success = 0,

        /// <summary>本步骤判定不需要执行（开关关闭、产物已最新等）。</summary>
        Skipped = 1,

        Failed = 2,
    }

    /// <summary>单步结果；失败信息会进汇总表，便于一眼看到是哪一步。</summary>
    public sealed class BuildStepResult
    {
        public BuildStepStatus Status { get; private set; }

        public string Message { get; private set; }

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

    /// <summary>步骤函数签名（静态方法即可，不需要为每个步骤建类）。</summary>
    public delegate BuildStepResult BuildStepHandler(BuildContext ctx);

    /// <summary>
    /// 一个打包步骤：名字 + 是否关键 + 静态方法。
    /// <para>
    /// 为什么用 <c>readonly struct</c> + 静态方法，而不是每个步骤一个类：
    /// </para>
    /// <list type="bullet">
    ///   <item>步骤是无状态的，每次打包 <c>new</c> 一批对象没有意义；</item>
    ///   <item>静态方法可以直接断点、可以直接被别处调用（不必先 new 出实例）；</item>
    ///   <item>目录表里 <c>BuildStep.Required("Xxx", AbBuildSteps.Xxx)</c> 比
    ///         <c>new XxxStep()</c> 更能一眼看出"顺序 + 是否关键"；</item>
    ///   <item>结构体不可变，列表可以做成 <c>static readonly</c>，零分配。</item>
    /// </list>
    /// </summary>
    public readonly struct BuildStep
    {
        /// <summary>日志与报告里显示的名字，保持唯一。</summary>
        public readonly string Name;

        /// <summary>失败时是否中止整个流程。</summary>
        public readonly bool IsRequired;

        public readonly BuildStepHandler Handler;

        private BuildStep(string name, bool required, BuildStepHandler handler)
        {
            this.Name = name;
            this.IsRequired = required;
            this.Handler = handler;
        }

        /// <summary>关键步骤：失败即中止（Lua bundle、AB 构建、清单、签名等）。</summary>
        public static BuildStep Required(string name, BuildStepHandler handler)
        {
            return new BuildStep(name, true, handler);
        }

        /// <summary>可降级步骤：失败只告警（图集、拷 StreamingAssets、弹窗等）。</summary>
        public static BuildStep Optional(string name, BuildStepHandler handler)
        {
            return new BuildStep(name, false, handler);
        }

        public BuildStepResult Run(BuildContext ctx)
        {
            return this.Handler(ctx);
        }
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
    /// 执行选项：只用于**调试**（只跑/跳过某些步骤）。
    /// <para>正常打包的范围与顺序由目录表决定，这才是唯一事实来源。</para>
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
