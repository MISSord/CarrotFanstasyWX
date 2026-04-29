using System;
using System.Collections.Generic;
using System.Linq;

namespace CarrotFantasy
{
    /// <summary>
    /// 通用加载编排器：
    /// - 统一管理场景、资源、UI、数据等异步任务
    /// - 支持阻塞关闭判定（blockClose）
    /// - 支持加权总进度、失败策略与超时
    /// 该类为纯逻辑层，当前不主动接入任何业务。
    /// </summary>
    public sealed class LoadingOrchestrator
    {
        private readonly Dictionary<int, LoadingTaskRuntime> _taskMap = new Dictionary<int, LoadingTaskRuntime>();
        private int _nextTaskId = 1;
        private float _planStartTimeSeconds;
        private bool _isRunning;
        private string _planName = string.Empty;
        private float _planTimeoutSeconds = -1f;

        public event Action<string> OnPlanStarted;
        public event Action<LoadingPlanSnapshot> OnPlanUpdated;
        public event Action<LoadingPlanResult> OnPlanFinished;

        public bool IsRunning => _isRunning;

        public void StartPlan(string planName, float timeoutSeconds = -1f)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Loading plan is already running. Call FinishPlan/CancelPlan before starting a new one.");
            }

            _taskMap.Clear();
            _nextTaskId = 1;
            _isRunning = true;
            _planName = string.IsNullOrEmpty(planName) ? "UnnamedPlan" : planName;
            _planTimeoutSeconds = timeoutSeconds;
            _planStartTimeSeconds = UnityEngine.Time.realtimeSinceStartup;

            OnPlanStarted?.Invoke(_planName);
            RaisePlanUpdated();
        }

        public LoadingTaskHandle CreateTask(LoadingTaskOptions options)
        {
            EnsurePlanRunning();

            ValidateOptions(options);

            int taskId = _nextTaskId++;
            var runtime = new LoadingTaskRuntime(taskId, options, UnityEngine.Time.realtimeSinceStartup);
            _taskMap.Add(taskId, runtime);
            RaisePlanUpdated();
            return new LoadingTaskHandle(this, taskId);
        }

        /// <summary>
        /// 用于驱动超时检测。建议后续接入主循环定期调用。
        /// </summary>
        public void Tick()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_planTimeoutSeconds > 0f)
            {
                float elapsed = UnityEngine.Time.realtimeSinceStartup - _planStartTimeSeconds;
                if (elapsed >= _planTimeoutSeconds)
                {
                    MarkRunningBlockTasksAsTimeout();
                    FinishPlan(LoadingPlanFinishReason.Timeout);
                    return;
                }
            }

            RaisePlanUpdated();
        }

        public LoadingPlanSnapshot GetSnapshot()
        {
            var tasks = _taskMap.Values.Select(x => x.ToSnapshot()).ToList();
            float weightedProgress = ComputeWeightedProgress(tasks);
            bool canClose = CanCloseLoading(tasks);
            float elapsed = _isRunning ? UnityEngine.Time.realtimeSinceStartup - _planStartTimeSeconds : 0f;

            return new LoadingPlanSnapshot(_planName, _isRunning, elapsed, weightedProgress, canClose, tasks);
        }

        public void CancelPlan(string reason = null)
        {
            if (!_isRunning)
            {
                return;
            }

            foreach (var runtime in _taskMap.Values)
            {
                if (runtime.State == LoadingTaskState.Pending || runtime.State == LoadingTaskState.Running)
                {
                    runtime.State = LoadingTaskState.Cancelled;
                    runtime.Error = string.IsNullOrEmpty(reason) ? "Plan cancelled." : reason;
                    runtime.CompletedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
                }
            }

            FinishPlan(LoadingPlanFinishReason.Cancelled);
        }

        private void ValidateOptions(LoadingTaskOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.Name))
            {
                throw new ArgumentException("Task name can not be empty.", nameof(options));
            }

            if (options.Weight <= 0f)
            {
                throw new ArgumentException("Task weight must be greater than 0.", nameof(options));
            }
        }

        private void EnsurePlanRunning()
        {
            if (!_isRunning)
            {
                throw new InvalidOperationException("No active loading plan. Call StartPlan first.");
            }
        }

        private void MarkRunningBlockTasksAsTimeout()
        {
            foreach (var runtime in _taskMap.Values)
            {
                if (runtime.Options.BlockClose &&
                    (runtime.State == LoadingTaskState.Pending || runtime.State == LoadingTaskState.Running))
                {
                    runtime.State = LoadingTaskState.Timeout;
                    runtime.Error = "Task timed out.";
                    runtime.CompletedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
                }
            }
        }

        private void RaisePlanUpdated()
        {
            if (!_isRunning)
            {
                return;
            }

            OnPlanUpdated?.Invoke(GetSnapshot());

            if (CanAutoFinish())
            {
                FinishPlan(LoadingPlanFinishReason.Completed);
            }
        }

        private bool CanAutoFinish()
        {
            if (!_isRunning)
            {
                return false;
            }

            foreach (var runtime in _taskMap.Values)
            {
                if (!runtime.Options.BlockClose)
                {
                    continue;
                }

                if (!IsTaskTerminal(runtime.State))
                {
                    return false;
                }

                if (runtime.State == LoadingTaskState.Failed && runtime.Options.FailurePolicy == LoadingTaskFailurePolicy.FailFast)
                {
                    return true;
                }

                if (runtime.State == LoadingTaskState.Timeout && runtime.Options.FailurePolicy != LoadingTaskFailurePolicy.IgnoreAndContinue)
                {
                    return true;
                }
            }

            return _taskMap.Values.Any() && _taskMap.Values.Where(x => x.Options.BlockClose).All(x => IsTaskTerminal(x.State));
        }

        private void FinishPlan(LoadingPlanFinishReason reason)
        {
            if (!_isRunning)
            {
                return;
            }

            var snapshot = GetSnapshot();
            var result = new LoadingPlanResult(snapshot, reason);

            _isRunning = false;
            OnPlanFinished?.Invoke(result);
        }

        private static bool IsTaskTerminal(LoadingTaskState state)
        {
            return state == LoadingTaskState.Succeeded ||
                   state == LoadingTaskState.Failed ||
                   state == LoadingTaskState.Cancelled ||
                   state == LoadingTaskState.Timeout;
        }

        private static bool CanCloseLoading(List<LoadingTaskSnapshot> tasks)
        {
            var blockers = tasks.Where(x => x.BlockClose).ToList();
            if (blockers.Count == 0)
            {
                return true;
            }

            foreach (var task in blockers)
            {
                if (task.State == LoadingTaskState.Pending || task.State == LoadingTaskState.Running)
                {
                    return false;
                }

                if (task.State == LoadingTaskState.Failed && task.FailurePolicy == LoadingTaskFailurePolicy.FailFast)
                {
                    return true;
                }
            }

            return true;
        }

        private static float ComputeWeightedProgress(List<LoadingTaskSnapshot> tasks)
        {
            if (tasks.Count == 0)
            {
                return 0f;
            }

            float totalWeight = 0f;
            float weightedProgress = 0f;
            foreach (var task in tasks)
            {
                float weight = Math.Max(0.0001f, task.Weight);
                totalWeight += weight;
                weightedProgress += weight * Clamp01(task.Progress);
            }

            return totalWeight <= 0f ? 0f : Clamp01(weightedProgress / totalWeight);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private void TrySetRunning(int taskId)
        {
            if (!_taskMap.TryGetValue(taskId, out var runtime) || !_isRunning)
            {
                return;
            }

            if (runtime.State == LoadingTaskState.Pending)
            {
                runtime.State = LoadingTaskState.Running;
                runtime.StartedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
                RaisePlanUpdated();
            }
        }

        private void TryReportProgress(int taskId, float progress)
        {
            if (!_taskMap.TryGetValue(taskId, out var runtime) || !_isRunning)
            {
                return;
            }

            if (IsTaskTerminal(runtime.State))
            {
                return;
            }

            runtime.Progress = Clamp01(progress);
            if (runtime.State == LoadingTaskState.Pending)
            {
                runtime.State = LoadingTaskState.Running;
                runtime.StartedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
            }

            RaisePlanUpdated();
        }

        private void TryComplete(int taskId)
        {
            if (!_taskMap.TryGetValue(taskId, out var runtime) || !_isRunning)
            {
                return;
            }

            if (IsTaskTerminal(runtime.State))
            {
                return;
            }

            runtime.Progress = 1f;
            runtime.State = LoadingTaskState.Succeeded;
            runtime.CompletedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
            RaisePlanUpdated();
        }

        private void TryFail(int taskId, string error)
        {
            if (!_taskMap.TryGetValue(taskId, out var runtime) || !_isRunning)
            {
                return;
            }

            if (IsTaskTerminal(runtime.State))
            {
                return;
            }

            runtime.State = LoadingTaskState.Failed;
            runtime.Error = string.IsNullOrEmpty(error) ? "Task failed." : error;
            runtime.CompletedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
            RaisePlanUpdated();
        }

        private void TryCancel(int taskId, string reason)
        {
            if (!_taskMap.TryGetValue(taskId, out var runtime) || !_isRunning)
            {
                return;
            }

            if (IsTaskTerminal(runtime.State))
            {
                return;
            }

            runtime.State = LoadingTaskState.Cancelled;
            runtime.Error = string.IsNullOrEmpty(reason) ? "Task cancelled." : reason;
            runtime.CompletedAtSeconds = UnityEngine.Time.realtimeSinceStartup;
            RaisePlanUpdated();
        }

        public readonly struct LoadingTaskHandle
        {
            private readonly LoadingOrchestrator _owner;
            public readonly int TaskId;

            internal LoadingTaskHandle(LoadingOrchestrator owner, int taskId)
            {
                _owner = owner;
                TaskId = taskId;
            }

            public void Start()
            {
                _owner?.TrySetRunning(TaskId);
            }

            public void ReportProgress(float progress)
            {
                _owner?.TryReportProgress(TaskId, progress);
            }

            public void Complete()
            {
                _owner?.TryComplete(TaskId);
            }

            public void Fail(string error = null)
            {
                _owner?.TryFail(TaskId, error);
            }

            public void Cancel(string reason = null)
            {
                _owner?.TryCancel(TaskId, reason);
            }
        }

        private sealed class LoadingTaskRuntime
        {
            public int TaskId;
            public LoadingTaskOptions Options;
            public LoadingTaskState State;
            public float Progress;
            public string Error;
            public float CreatedAtSeconds;
            public float StartedAtSeconds;
            public float CompletedAtSeconds;

            public LoadingTaskRuntime(int taskId, LoadingTaskOptions options, float createdAtSeconds)
            {
                TaskId = taskId;
                Options = options;
                State = LoadingTaskState.Pending;
                Progress = 0f;
                Error = string.Empty;
                CreatedAtSeconds = createdAtSeconds;
                StartedAtSeconds = -1f;
                CompletedAtSeconds = -1f;
            }

            public LoadingTaskSnapshot ToSnapshot()
            {
                return new LoadingTaskSnapshot(
                    TaskId,
                    Options.Name,
                    Options.Kind,
                    Options.Group,
                    Options.BlockClose,
                    Options.Weight,
                    Options.FailurePolicy,
                    State,
                    Progress,
                    Error,
                    CreatedAtSeconds,
                    StartedAtSeconds,
                    CompletedAtSeconds);
            }
        }
    }

    public sealed class LoadingTaskOptions
    {
        public string Name;
        public LoadingTaskKind Kind = LoadingTaskKind.Custom;
        public string Group = "Default";
        public bool BlockClose = true;
        public float Weight = 1f;
        public LoadingTaskFailurePolicy FailurePolicy = LoadingTaskFailurePolicy.RetryThenFail;
    }

    public enum LoadingTaskKind
    {
        Scene = 0,
        Asset = 1,
        Ui = 2,
        Data = 3,
        Network = 4,
        Custom = 100
    }

    public enum LoadingTaskFailurePolicy
    {
        FailFast = 0,
        RetryThenFail = 1,
        IgnoreAndContinue = 2
    }

    public enum LoadingTaskState
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        Timeout = 5
    }

    public enum LoadingPlanFinishReason
    {
        Completed = 0,
        Cancelled = 1,
        Timeout = 2
    }

    public sealed class LoadingTaskSnapshot
    {
        public readonly int TaskId;
        public readonly string Name;
        public readonly LoadingTaskKind Kind;
        public readonly string Group;
        public readonly bool BlockClose;
        public readonly float Weight;
        public readonly LoadingTaskFailurePolicy FailurePolicy;
        public readonly LoadingTaskState State;
        public readonly float Progress;
        public readonly string Error;
        public readonly float CreatedAtSeconds;
        public readonly float StartedAtSeconds;
        public readonly float CompletedAtSeconds;

        public LoadingTaskSnapshot(
            int taskId,
            string name,
            LoadingTaskKind kind,
            string group,
            bool blockClose,
            float weight,
            LoadingTaskFailurePolicy failurePolicy,
            LoadingTaskState state,
            float progress,
            string error,
            float createdAtSeconds,
            float startedAtSeconds,
            float completedAtSeconds)
        {
            TaskId = taskId;
            Name = name;
            Kind = kind;
            Group = group;
            BlockClose = blockClose;
            Weight = weight;
            FailurePolicy = failurePolicy;
            State = state;
            Progress = progress;
            Error = error;
            CreatedAtSeconds = createdAtSeconds;
            StartedAtSeconds = startedAtSeconds;
            CompletedAtSeconds = completedAtSeconds;
        }
    }

    public sealed class LoadingPlanSnapshot
    {
        public readonly string PlanName;
        public readonly bool IsRunning;
        public readonly float ElapsedSeconds;
        public readonly float WeightedProgress;
        public readonly bool CanCloseLoading;
        public readonly List<LoadingTaskSnapshot> Tasks;

        public LoadingPlanSnapshot(
            string planName,
            bool isRunning,
            float elapsedSeconds,
            float weightedProgress,
            bool canCloseLoading,
            List<LoadingTaskSnapshot> tasks)
        {
            PlanName = planName;
            IsRunning = isRunning;
            ElapsedSeconds = elapsedSeconds;
            WeightedProgress = weightedProgress;
            CanCloseLoading = canCloseLoading;
            Tasks = tasks ?? new List<LoadingTaskSnapshot>();
        }
    }

    public sealed class LoadingPlanResult
    {
        public readonly LoadingPlanSnapshot Snapshot;
        public readonly LoadingPlanFinishReason FinishReason;

        public LoadingPlanResult(LoadingPlanSnapshot snapshot, LoadingPlanFinishReason finishReason)
        {
            Snapshot = snapshot;
            FinishReason = finishReason;
        }
    }
}
