using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 启动期资源更新的**流程状态**。由 <see cref="UpdateFlow"/> 状态机推进。
    /// </summary>
    public enum UpdateFlowState
    {
        Idle = 0,

        /// <summary>拉远程清单 + 验签 + 比对 + 完整性校验。</summary>
        Checking,

        /// <summary>远程不可用：注入本地缓存 / 包内基线清单，并询问玩家是否继续。</summary>
        CheckFallback,

        /// <summary>等待玩家确认下载。</summary>
        Confirming,

        /// <summary>下载 + 解包 + LZMA→LZ4 转换。</summary>
        Downloading,

        /// <summary>收尾：结束下载器、关闭进度面板、保存清单。</summary>
        Finalizing,

        /// <summary>终态：资源就绪。</summary>
        Done,

        /// <summary>终态：失败（已在 Failed 状态提示过玩家）。</summary>
        Failed,

        /// <summary>终态：取消（已退出进程）。</summary>
        Cancelled,
    }

    /// <summary>状态对"这一帧之后往哪走"的回答。</summary>
    public enum StepResult
    {
        /// <summary>还要继续，下一帧再来。</summary>
        Running,

        /// <summary>本状态做完了，按 <see cref="UpdateContext.NextState"/> 转移。</summary>
        Advance,

        /// <summary>失败，转 <see cref="UpdateFlowState.Failed"/>，用 <see cref="UpdateContext.Error"/> 说明原因。</summary>
        Fail,

        /// <summary>取消，转 <see cref="UpdateFlowState.Cancelled"/>。</summary>
        Cancel,
    }

    /// <summary>
    /// 跨状态共享的上下文。状态之间**不直接通信**，只通过这里交换数据。
    /// </summary>
    public sealed class UpdateContext
    {
        public MonoBehaviour Host;

        public Transform Parent;

        public AssetBundleDownloader Downloader;

        /// <summary>检查结果（Checking 之后所有状态都可读）。</summary>
        public UpdateCheckResult CheckResult;

        /// <summary>本状态想去的下一个状态（配合 <see cref="StepResult.Advance"/>）。</summary>
        public UpdateFlowState NextState;

        /// <summary>失败原因（配合 <see cref="StepResult.Fail"/>）。</summary>
        public string Error;

        /// <summary>本次下载是否成功（Finalize 阶段读取）。</summary>
        public bool DownloadSucceeded;

        /// <summary>所属状态机（状态内部用它设置进度/描述/完成）。</summary>
        public UpdateFlow Flow;
    }

    /// <summary>
    /// 任务协议：状态内部用它干活。
    /// <para>
    /// 契约：<see cref="Tick"/> 每帧被调用一次，内部**不许阻塞、不许 yield、不许 while 等待**。
    /// 异步工作要么是可轮询对象（UnityWebRequest / 下载器），要么把完成信号写进字段。
    /// </para>
    /// </summary>
    public interface IUpdateTask
    {
        /// <summary>进入状态时调用一次。</summary>
        void Start(UpdateContext ctx);

        /// <summary>每帧推进一次。</summary>
        void Tick(UpdateContext ctx);

        /// <summary>是否已完成。</summary>
        bool IsDone { get; }

        /// <summary>完成时是否成功。</summary>
        bool IsSucceeded { get; }

        /// <summary>失败原因。</summary>
        string Error { get; }

        /// <summary>状态被提前打断 / 机器关闭时清理资源。</summary>
        void Abort();
    }

    /// <summary>状态协议。每个状态只做一件事，并明确回答"下一步去哪"。</summary>
    public interface IUpdateState
    {
        UpdateFlowState Id { get; }

        void Enter(UpdateContext ctx);

        StepResult Tick(UpdateContext ctx);

        void Exit(UpdateContext ctx);
    }

    /// <summary>
    /// 启动期资源更新的**驱动者**（大状态机）。
    /// <para>
    /// 设计要点：
    /// 1. **只有它能推进状态**：状态之间靠 <see cref="StepResult"/> 回答，转移只发生在 <see cref="Tick"/> 里。
    /// 2. **每个状态内部用 <see cref="IUpdateTask"/> 干活**，状态本身只管"做完没、失败没、下一步去哪"。
    /// 3. **终态自动关闭**：进入 Done / Failed / Cancelled 后 <see cref="IsRunning"/> 变 false，
    ///    <see cref="Tick"/> 第一行就返回（零开销），等价于"这个状态机不存在了"。
    /// 4. 保留转移历史与卡死检测，失败时可直接导出"卡在哪一步"。
    /// </para>
    /// </summary>
    public sealed class UpdateFlow
    {
        private const int MaxTrace = 16;

        private static UpdateFlow current;

        private readonly Dictionary<UpdateFlowState, IUpdateState> states =
            new Dictionary<UpdateFlowState, IUpdateState>();

        private readonly List<string> trace = new List<string>(MaxTrace);

        private UpdateStateMachineContext machineContext;

        private IUpdateState currentState;
        private IUpdateTask currentTask;

        private float flowStartedAt;
        private float stateEnteredAt;
        private bool stuckLogged;

        /// <summary>当前活动实例（UI 与 GameMain 通过它读状态）。</summary>
        public static UpdateFlow Current
        {
            get { return current; }
        }

        /// <summary>是否还在运行。false 表示状态机已关闭，Tick 不再有任何开销。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>是否已经结束（含成功与失败）。</summary>
        public bool HasFinished { get; private set; }

        /// <summary>终态结果（仅 HasFinished 后有效）。</summary>
        public AotResourceUpdateRunner.Result Result { get; private set; }

        /// <summary>当前状态。只能由状态机推进。</summary>
        public UpdateFlowState State { get; private set; }

        /// <summary>当前阶段进度 0~1（UI 进度条用）。</summary>
        public float Progress { get; private set; }

        /// <summary>当前阶段可读描述。</summary>
        public string Operation { get; private set; }

        /// <summary>失败原因。</summary>
        public string Error { get; private set; }

        /// <summary>状态变化事件：(from, to)。UI 订阅它，不再每帧轮询。</summary>
        public event Action<UpdateFlowState, UpdateFlowState> StateChanged;

        /// <summary>是否处于"忙"的阶段。</summary>
        public bool IsBusy
        {
            get
            {
                return this.State == UpdateFlowState.Checking
                       || this.State == UpdateFlowState.CheckFallback
                       || this.State == UpdateFlowState.Confirming
                       || this.State == UpdateFlowState.Downloading
                       || this.State == UpdateFlowState.Finalizing;
            }
        }

        /// <summary>当前状态已持续秒数。</summary>
        public float StateElapsed
        {
            get { return Time.realtimeSinceStartup - this.stateEnteredAt; }
        }

        /// <summary>整个流程已持续秒数。</summary>
        public float TotalElapsed
        {
            get { return Time.realtimeSinceStartup - this.flowStartedAt; }
        }

        /// <summary>最近转移记录（新的在后）。</summary>
        public IReadOnlyList<string> Trace
        {
            get { return this.trace; }
        }

        /// <summary>
        /// 创建一个已注册全部状态的状态机实例。重复创建会覆盖 <see cref="Current"/>。
        /// </summary>
        public static UpdateFlow Create(UpdateContext ctx)
        {
            UpdateFlow flow = new UpdateFlow();
            flow.machineContext = new UpdateStateMachineContext(ctx, flow);

            flow.Register(new PrepareState());
            flow.Register(new CheckState());
            flow.Register(new CheckFallbackState());
            flow.Register(new ConfirmState());
            flow.Register(new DownloadState());
            flow.Register(new FinalizeState());
            flow.Register(new FailedState());

            current = flow;
            return flow;
        }

        /// <summary>注册一个状态（必须覆盖流程中出现过的所有非终态）。</summary>
        public void Register(IUpdateState state)
        {
            if (state == null)
            {
                return;
            }

            this.states[state.Id] = state;
        }

        /// <summary>启动：从 <paramref name="initial"/> 进入。</summary>
        /// <summary>启动状态机。入口固定为 Idle（PrepareState）。</summary>
        public void Start()
        {
            this.trace.Clear();
            this.Error = null;
            this.Progress = 0f;
            this.Operation = string.Empty;
            this.HasFinished = false;
            this.IsRunning = true;
            this.stuckLogged = false;

            this.flowStartedAt = Time.realtimeSinceStartup;
            this.stateEnteredAt = this.flowStartedAt;
            this.State = UpdateFlowState.Idle;

            Debug.Log("[UpdateFlow] 状态机启动（入口状态 Idle）");
            this.EnterState(UpdateFlowState.Idle, true);
        }

        /// <summary>每帧驱动。机器关闭后直接返回。</summary>
        public void Tick()
        {
            if (!this.IsRunning || this.currentState == null)
            {
                return;
            }

            UpdateContext ctx = this.machineContext.Context;
            ctx.NextState = this.State;

            StepResult result = this.currentState.Tick(ctx);

            switch (result)
            {
                case StepResult.Running:
                    break;

                case StepResult.Advance:
                    if (ctx.NextState == this.State)
                    {
                        // 防呆：状态原地"前进"等于没前进，会死循环而不报错。
                        ctx.Error = "状态机内部错误：状态原地转移（" + this.State + "）";
                        Debug.LogError("[UpdateFlow] " + ctx.Error);
                        this.EnterState(UpdateFlowState.Failed);
                    }
                    else
                    {
                        this.EnterState(ctx.NextState);
                    }

                    break;

                case StepResult.Fail:
                    this.Error = string.IsNullOrEmpty(ctx.Error) ? "未知错误" : ctx.Error;
                    Debug.LogError("[UpdateFlow] 状态 " + this.State + " 失败：" + this.Error + "\n" + this.DumpTrace());
                    this.EnterState(UpdateFlowState.Failed);
                    break;

                case StepResult.Cancel:
                    this.EnterState(UpdateFlowState.Cancelled);
                    break;
            }
        }

        /// <summary>由状态在终态时调用：记录结果并关闭状态机。</summary>
        public void Complete(AotResourceUpdateRunner.Result result)
        {
            if (this.HasFinished)
            {
                return;
            }

            this.Result = result;
            this.HasFinished = true;
            this.Stop();

            Debug.Log(string.Format(
                "[UpdateFlow] 状态机结束：result={0} 总耗时={1:F2}s\n{2}",
                result,
                this.TotalElapsed,
                this.DumpTrace()));
        }

        /// <summary>关闭状态机：打断当前状态并释放引用。</summary>
        public void Stop()
        {
            if (!this.IsRunning)
            {
                return;
            }

            this.IsRunning = false;

            if (this.currentState != null)
            {
                try
                {
                    this.currentState.Exit(this.machineContext.Context);
                }
                catch (Exception e)
                {
                    Debug.LogError("[UpdateFlow] 状态 Exit 异常: " + e);
                }
            }

            if (this.currentTask != null)
            {
                try
                {
                    this.currentTask.Abort();
                }
                catch (Exception e)
                {
                    Debug.LogError("[UpdateFlow] 任务 Abort 异常: " + e);
                }
            }

            this.currentTask = null;
            this.currentState = null;

            Debug.Log("[UpdateFlow] 状态机已关闭，Tick 不再产生开销");
        }

        /// <summary>设置进度（任务调用）。</summary>
        public void SetProgress(float progress)
        {
            this.Progress = Mathf.Clamp01(progress);
        }

        /// <summary>更新描述（任务调用，不触发状态转移）。</summary>
        public void SetOperation(string operation)
        {
            if (!string.IsNullOrEmpty(operation))
            {
                this.Operation = operation;
            }
        }

        /// <summary>卡死检测：同一状态超过阈值秒数告警一次。</summary>
        public void LogIfStuck(float thresholdSeconds)
        {
            if (this.stuckLogged || !this.IsBusy)
            {
                return;
            }

            if (this.StateElapsed < thresholdSeconds)
            {
                return;
            }

            this.stuckLogged = true;
            Debug.LogWarning(string.Format(
                "[UpdateFlow] 疑似卡住：状态 {0} 已持续 {1:F1}s（阈值 {2:F1}s）op={3}\n{4}",
                this.State,
                this.StateElapsed,
                thresholdSeconds,
                this.Operation,
                this.DumpTrace()));
        }

        /// <summary>一行摘要。</summary>
        public string Describe()
        {
            return string.Format(
                "state={0} running={1} op={2} progress={3:P0} stateElapsed={4:F1}s totalElapsed={5:F1}s{6}",
                this.State,
                this.IsRunning,
                string.IsNullOrEmpty(this.Operation) ? "-" : this.Operation,
                this.Progress,
                this.StateElapsed,
                this.TotalElapsed,
                string.IsNullOrEmpty(this.Error) ? string.Empty : " error=" + this.Error);
        }

        /// <summary>最近的转移记录。</summary>
        public string DumpTrace()
        {
            if (this.trace.Count == 0)
            {
                return "[UpdateFlow] (无转移记录)";
            }

            StringBuilder sb = new StringBuilder("[UpdateFlow] 最近状态转移：");
            for (int i = 0; i < this.trace.Count; i++)
            {
                sb.Append('\n').Append("  ").Append(this.trace[i]);
            }

            return sb.ToString();
        }

        /// <summary>供状态登记它启动的任务，便于统一 Abort。</summary>
        internal void AttachTask(IUpdateTask task)
        {
            this.currentTask = task;
        }

        private void EnterState(UpdateFlowState next, bool initial = false)
        {
            UpdateFlowState from = this.State;

            if (this.currentState != null)
            {
                try
                {
                    this.currentState.Exit(this.machineContext.Context);
                }
                catch (Exception e)
                {
                    Debug.LogError("[UpdateFlow] " + from + " Exit 异常: " + e);
                }
            }

            this.currentTask = null;

            // 真终态：进入即关闭，不再 Tick。
            // 注意：Failed **不在这里**——它要弹面板等玩家点按，必须走正常状态路径。
            if (next == UpdateFlowState.Done
                || next == UpdateFlowState.Cancelled)
            {
                this.State = next;
                this.Progress = next == UpdateFlowState.Done ? 1f : this.Progress;
                this.AppendTrace(string.Format("{0,7:F2}s  {1} -> {2}（终态）", this.TotalElapsed, from, next));
                Debug.Log(string.Format("[UpdateFlow] {0} -> {1}（终态）累计={2:F2}s",
                    from, next, this.TotalElapsed));
                this.RaiseStateChanged(from, next);

                if (next == UpdateFlowState.Cancelled)
                {
                    this.Result = AotResourceUpdateRunner.Result.Cancelled;
                    this.HasFinished = true;
                    this.Stop();
                }
                else if (next == UpdateFlowState.Done)
                {
                    this.Result = AotResourceUpdateRunner.Result.Success;
                    this.HasFinished = true;
                    this.Stop();
                }

                return;
            }

            IUpdateState state;
            if (!this.states.TryGetValue(next, out state))
            {
                Debug.LogError("[UpdateFlow] 未注册的状态: " + next + "，按失败处理");
                this.EnterState(UpdateFlowState.Failed);
                return;
            }

            this.State = next;
            this.Progress = 0f;
            this.stuckLogged = false;
            this.stateEnteredAt = Time.realtimeSinceStartup;
            this.currentState = state;

            if (initial)
            {
                // 首次进入不是"转移"，单独记一条，避免出现 Idle -> Idle 这种假转移。
                this.AppendTrace(string.Format("{0,7:F2}s  初始化 -> {1}", this.TotalElapsed, next));
                Debug.Log(string.Format("[UpdateFlow] 初始化进入 {0}", next));
            }
            else
            {
                this.AppendTrace(string.Format("{0,7:F2}s  {1} -> {2}", this.TotalElapsed, from, next));
                Debug.Log(string.Format("[UpdateFlow] {0} -> {1}  累计={2:F2}s", from, next, this.TotalElapsed));
            }

            if (!initial)
            {
                this.RaiseStateChanged(from, next);
            }

            try
            {
                state.Enter(this.machineContext.Context);
            }
            catch (Exception e)
            {
                Debug.LogError("[UpdateFlow] " + next + " Enter 异常: " + e);
                this.Error = e.Message;
                this.EnterState(UpdateFlowState.Failed);
            }
        }

        private void RaiseStateChanged(UpdateFlowState from, UpdateFlowState to)
        {
            Action<UpdateFlowState, UpdateFlowState> handler = this.StateChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(from, to);
            }
            catch (Exception e)
            {
                Debug.LogError("[UpdateFlow] StateChanged 回调异常: " + e);
            }
        }

        private void AppendTrace(string line)
        {
            if (this.trace.Count >= MaxTrace)
            {
                this.trace.RemoveAt(0);
            }

            this.trace.Add(line);
        }

        /// <summary>给状态用的薄封装：把 flow 与 ctx 绑在一起，省掉重复传参。</summary>
        private sealed class UpdateStateMachineContext
        {
            public UpdateStateMachineContext(UpdateContext context, UpdateFlow flow)
            {
                this.Context = context;
                this.Context.Flow = flow;
            }

            public UpdateContext Context { get; private set; }
        }
    }
}
