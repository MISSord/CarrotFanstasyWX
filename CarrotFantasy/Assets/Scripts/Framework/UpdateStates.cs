using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    // =====================================================================
    // 状态：只回答"做完没 / 失败没 / 下一步去哪"，具体活交给任务
    // =====================================================================

    /// <summary>
    /// 准备：处理编辑器直读与测试模式，决定从哪进入正式流程。
    /// </summary>
    internal sealed class PrepareState : IUpdateState
    {
        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Idle; }
        }

        public void Enter(UpdateContext ctx)
        {
        }

        public void Exit(UpdateContext ctx)
        {
        }

        public StepResult Tick(UpdateContext ctx)
        {
#if UNITY_EDITOR
            LoadMode loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
            Debug.Log("[UpdateFlow] Prepare: LoadMode=" + loadMode + "（菜单 Tools/编辑器/游戏加载模式设置）");

            if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
            {
                Debug.Log("[UpdateFlow] 当前为 " + loadMode + "：编辑器直读资源，跳过检查与下载");
                ctx.Flow.SetOperation("编辑器直读：跳过资源更新");
                ctx.NextState = UpdateFlowState.Done;
                return StepResult.Advance;
            }

            if (loadMode == LoadMode.Testing
                && AssetBundleUpdateChecker.TryBootstrapTestingCheck(out UpdateCheckResult testingResult))
            {
                AssetBundleManager.Instance.SetAssetBundleItem(testingResult.customManifest);
                ctx.CheckResult = testingResult;

                if (!testingResult.hasChanges)
                {
                    ctx.Flow.SetOperation("测试模式：资源已是最新");
                    ctx.NextState = UpdateFlowState.Done;
                    return StepResult.Advance;
                }

                ctx.NextState = UpdateFlowState.Confirming;
                return StepResult.Advance;
            }
#endif
            ctx.NextState = UpdateFlowState.Checking;
            return StepResult.Advance;
        }
    }

    /// <summary>检查远程清单并比对本地状态。</summary>
    internal sealed class CheckState : IUpdateState
    {
        private CheckTask task;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Checking; }
        }

        public void Enter(UpdateContext ctx)
        {
            ctx.Flow.SetOperation("检查资源版本");
            this.task = new CheckTask();
            this.task.Start(ctx);
        }

        public void Exit(UpdateContext ctx)
        {
            if (this.task != null)
            {
                this.task.Abort();
                this.task = null;
            }
        }

        public StepResult Tick(UpdateContext ctx)
        {
            this.task.Tick(ctx);

            if (!this.task.IsDone)
            {
                // 子阶段（下载清单 → 比对 → 校验）检查器已经暴露，直接映射到状态机的
                // 描述与进度，不必为此新增状态。
                ctx.Flow.SetOperation(this.task.Operation);
                ctx.Flow.SetProgress(this.task.Progress);
                ctx.Flow.LogIfStuck(30f);
                return StepResult.Running;
            }

            bool checkOk = this.task.IsSucceeded
                           && ctx.CheckResult != null
                           && ctx.CheckResult.isSuccess;

            if (!checkOk || IsManifestEmpty(ctx.CheckResult))
            {
                if (AotResourceUpdateRunner.TryInjectCachedLocalManifest()
                    || AotResourceUpdateRunner.TryInjectBundledManifest())
                {
                    ctx.NextState = UpdateFlowState.CheckFallback;
                    return StepResult.Advance;
                }

                ctx.Error = checkOk ? "清单为空，无法继续" : "资源更新检查失败（无可用本地清单）";
                return StepResult.Fail;
            }

            if (!ctx.CheckResult.hasChanges)
            {
                AssetBundleManager.Instance.SetAssetBundleItem(ctx.CheckResult.customManifest);
                ctx.Flow.SetOperation("资源已是最新");
                ctx.NextState = UpdateFlowState.Done;
                return StepResult.Advance;
            }

            ctx.NextState = UpdateFlowState.Confirming;
            return StepResult.Advance;
        }

        private static bool IsManifestEmpty(UpdateCheckResult result)
        {
            return result == null
                   || result.customManifest == null
                   || result.customManifest.AssetBundles == null
                   || result.customManifest.AssetBundles.Count == 0;
        }
    }

    /// <summary>已回退到本地/内置清单：询问玩家继续还是退出。</summary>
    internal sealed class CheckFallbackState : IUpdateState
    {
        private FallbackTask task;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.CheckFallback; }
        }

        public void Enter(UpdateContext ctx)
        {
            ctx.Flow.SetOperation("已回退到本地缓存清单");
            this.task = new FallbackTask();
            this.task.Start(ctx);
        }

        public void Exit(UpdateContext ctx)
        {
            if (this.task != null)
            {
                this.task.Abort();
                this.task = null;
            }
        }

        public StepResult Tick(UpdateContext ctx)
        {
            this.task.Tick(ctx);

            if (!this.task.IsDone)
            {
                return StepResult.Running;
            }

            if (this.task.IsSucceeded)
            {
                ctx.Flow.SetOperation("使用本地清单进入游戏");
                ctx.NextState = UpdateFlowState.Done;
                return StepResult.Advance;
            }

            AotResourceUpdateRunner.QuitApp();
            return StepResult.Cancel;
        }
    }

    /// <summary>等待玩家确认下载。</summary>
    internal sealed class ConfirmState : IUpdateState
    {
        private ConfirmTask task;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Confirming; }
        }

        public void Enter(UpdateContext ctx)
        {
            ctx.Flow.SetOperation("等待确认下载");
            this.task = new ConfirmTask();
            this.task.Start(ctx);
        }

        public void Exit(UpdateContext ctx)
        {
            if (this.task != null)
            {
                this.task.Abort();
                this.task = null;
            }
        }

        public StepResult Tick(UpdateContext ctx)
        {
            this.task.Tick(ctx);

            if (!this.task.IsDone)
            {
                return StepResult.Running;
            }

            if (this.task.IsSucceeded)
            {
                ctx.NextState = UpdateFlowState.Downloading;
                return StepResult.Advance;
            }

            AotResourceUpdateRunner.QuitApp();
            return StepResult.Cancel;
        }
    }

    /// <summary>下载 + 解包 + 转换。</summary>
    internal sealed class DownloadState : IUpdateState
    {
        private BundleDownloadTask task;
        private AotDownloadProgressView progressView;
        private bool openFailed;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Downloading; }
        }

        public void Enter(UpdateContext ctx)
        {
            AotBootUi.EnsureEventSystem();

            this.task = new BundleDownloadTask();
            this.task.Start(ctx);

            this.progressView = new AotDownloadProgressView();
            if (!this.progressView.Open(AssetBundleDownloader.Instance, ctx.Parent))
            {
                this.openFailed = true;
            }

            // 面板要跨到 Converting 阶段继续显示，放进 ctx 交给后续状态关闭。
            ctx.ProgressView = this.progressView;
        }

        public void Exit(UpdateContext ctx)
        {
            // 进度面板不在这里关闭：解压阶段还要继续显示，
            // 由 FinalizeState / FailedState 统一收尾。
            this.progressView = null;

            if (this.task != null)
            {
                this.task.Abort();
                this.task = null;
            }
        }

        public StepResult Tick(UpdateContext ctx)
        {
            if (this.openFailed)
            {
                ctx.Error = "无法打开下载进度面板";
                return StepResult.Fail;
            }

            this.task.Tick(ctx);
            ctx.Flow.SetProgress(this.task.Progress);
            ctx.Flow.LogIfStuck(120f);

            if (this.progressView != null)
            {
                this.progressView.Refresh();
            }

            if (!this.task.IsDone)
            {
                return StepResult.Running;
            }

            // 下载器「下载阶段不解压」，进入 Convert 说明还有一整段解压/转换要做，
            // 因此这里分流到独立的 Converting 状态，而不是直接进 Finalizing。
            ctx.NextState = this.task.CurrentLoaderState == LoaderState.Convert
                ? UpdateFlowState.Converting
                : UpdateFlowState.Finalizing;
            return StepResult.Advance;
        }
    }

    /// <summary>
    /// 解压 / 转换阶段：下载全部完成后才开始（下载期间不解压，避免 IO 过大）。
    /// 转换任务完成后才触发下载完成回调，所以这里以「回调到达 + 下载器回 Idle」为退出条件。
    /// </summary>
    internal sealed class ConvertingState : IUpdateState
    {
        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Converting; }
        }

        public void Enter(UpdateContext ctx)
        {
            ctx.Flow.SetOperation("解压中，请稍候...");
            ctx.Flow.SetProgress(1f);
        }

        public void Exit(UpdateContext ctx)
        {
        }

        public StepResult Tick(UpdateContext ctx)
        {
            // 下载器是单例（Instance 懒创建，不会为 null），不需要经 ctx 中转。
            AssetBundleDownloader downloader = AssetBundleDownloader.Instance;

            if (ctx.ProgressView != null)
            {
                ctx.ProgressView.Refresh();
            }

            if (ctx.DownloadFinished && downloader.GetLoaderState() == LoaderState.Idle)
            {
                ctx.NextState = UpdateFlowState.Finalizing;
                return StepResult.Advance;
            }

            // 解压/转换的单包超时阈值与下载不同，单独设。
            ctx.Flow.LogIfStuck(60f);
            return StepResult.Running;
        }
    }

    /// <summary>收尾：结束下载器、校验最终结果。</summary>
    internal sealed class FinalizeState : IUpdateState
    {
        private bool ended;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Finalizing; }
        }

        public void Enter(UpdateContext ctx)
        {
            ctx.Flow.SetOperation("收尾：结束下载器");
            ctx.Flow.SetProgress(1f);

            if (ctx.ProgressView != null)
            {
                ctx.ProgressView.Close();
                ctx.ProgressView = null;
            }
        }

        public void Exit(UpdateContext ctx)
        {
        }

        public StepResult Tick(UpdateContext ctx)
        {
            if (!this.ended)
            {
                this.ended = true;
                AssetBundleDownloader.Instance.EndDownload();
            }

            if (!ctx.DownloadSucceeded)
            {
                ctx.Error = "资源下载或校验失败";
                return StepResult.Fail;
            }

            ctx.Flow.SetOperation("资源更新完成");
            ctx.NextState = UpdateFlowState.Done;
            return StepResult.Advance;
        }
    }

    /// <summary>失败：弹面板等玩家点按，再结束进程。</summary>
    internal sealed class FailedState : IUpdateState
    {
        private ErrorDialogTask task;

        public UpdateFlowState Id
        {
            get { return UpdateFlowState.Failed; }
        }

        public void Enter(UpdateContext ctx)
        {
            if (ctx.ProgressView != null)
            {
                ctx.ProgressView.Close();
                ctx.ProgressView = null;
            }

            ctx.Flow.SetOperation(string.IsNullOrEmpty(ctx.Error) ? "资源更新失败" : ctx.Error);
            this.task = new ErrorDialogTask();
            this.task.Start(ctx);
        }

        public void Exit(UpdateContext ctx)
        {
            if (this.task != null)
            {
                this.task.Abort();
                this.task = null;
            }
        }

        public StepResult Tick(UpdateContext ctx)
        {
            this.task.Tick(ctx);

            if (!this.task.IsDone)
            {
                return StepResult.Running;
            }

            AotResourceUpdateRunner.QuitApp();
            ctx.Flow.Complete(AotResourceUpdateRunner.Result.Failed);
            return StepResult.Running;
        }
    }

    // =====================================================================
    // 任务：状态内部的具体工作，全部为"可轮询"，不阻塞、不 yield
    // =====================================================================

    /// <summary>把 <see cref="AssetBundleUpdateChecker"/> 包成可轮询任务。</summary>
    internal sealed class CheckTask : IUpdateTask
    {
        private AssetBundleUpdateChecker checker;
        private bool callbackFired;
        private bool ended;
        private UpdateCheckResult result;

        public bool IsDone
        {
            get { return this.ended; }
        }

        public bool IsSucceeded
        {
            get { return this.result != null && this.result.isSuccess; }
        }

        /// <summary>子阶段进度（直接读检查器，不做拷贝）。</summary>
        public float Progress
        {
            get { return this.checker != null ? this.checker.Progress : 0f; }
        }

        /// <summary>子阶段描述，如"对比清单文件... (37/120)"。</summary>
        public string Operation
        {
            get { return this.checker != null ? this.checker.CurrentOperation : string.Empty; }
        }

        public string Error { get; private set; }

        public void Start(UpdateContext ctx)
        {
            this.checker = new AssetBundleUpdateChecker();
            this.checker.StartUpdateCheck(
                AssetBundlePathHelper.GetServerLoadUrl(),
                r =>
                {
                    this.result = r;
                    this.callbackFired = true;
                });
        }

        public void Tick(UpdateContext ctx)
        {
            if (this.ended)
            {
                return;
            }

            this.checker.Update();

            if (!this.callbackFired || this.checker.IsRunning)
            {
                return;
            }

            this.checker.EndCheck();
            this.ended = true;

            ctx.CheckResult = this.result;

            if (this.result == null)
            {
                this.Error = "检查结果为 null";
            }
            else if (!this.result.isSuccess)
            {
                this.Error = string.IsNullOrEmpty(this.result.currentOperation)
                    ? "检查失败"
                    : this.result.currentOperation;
            }
        }

        public void Abort()
        {
            if (this.ended)
            {
                return;
            }

            this.ended = true;
            if (this.checker != null)
            {
                try
                {
                    this.checker.EndCheck();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[UpdateFlow] CheckTask.Abort: " + e.Message);
                }
            }
        }
    }

    /// <summary>回退提示面板。完成条件由按钮回调写入，状态机只轮询。</summary>
    internal sealed class FallbackTask : IUpdateTask
    {
        private AotUpdateListFallbackView view;

        /// <summary>0=未选 1=继续 2=退出</summary>
        private int choice;

        public bool IsDone
        {
            get { return this.choice != 0; }
        }

        public bool IsSucceeded
        {
            get { return this.choice == 1; }
        }

        public string Error { get; private set; }

        public void Start(UpdateContext ctx)
        {
            AotBootUi.EnsureEventSystem();

            this.view = new AotUpdateListFallbackView();
            if (!this.view.Open(
                    () => this.choice = 1,
                    () => this.choice = 2,
                    ctx.Parent))
            {
                this.Error = "无法显示回退提示面板";
                this.choice = 2;
            }
        }

        public void Tick(UpdateContext ctx)
        {
        }

        public void Abort()
        {
            if (this.view != null)
            {
                this.view.Close();
                this.view = null;
            }
        }
    }

    /// <summary>下载确认面板。</summary>
    internal sealed class ConfirmTask : IUpdateTask
    {
        private AotDownloadConfirmView view;

        private int choice;

        public bool IsDone
        {
            get { return this.choice != 0; }
        }

        public bool IsSucceeded
        {
            get { return this.choice == 1; }
        }

        public string Error { get; private set; }

        public void Start(UpdateContext ctx)
        {
            AotBootUi.EnsureEventSystem();

            long totalSize = ctx.CheckResult != null ? ctx.CheckResult.totalDownloadSize : 0L;

            this.view = new AotDownloadConfirmView();
            if (!this.view.Open(
                    totalSize,
                    () => this.choice = 1,
                    () => this.choice = 2,
                    ctx.Parent))
            {
                this.Error = "无法显示下载确认面板";
                this.choice = 2;
            }
        }

        public void Tick(UpdateContext ctx)
        {
        }

        public void Abort()
        {
            if (this.view != null)
            {
                this.view.Close();
                this.view = null;
            }
        }
    }

    /// <summary>
    /// 下载任务：只**读**下载器状态，不驱动它（下载器由 AssetBundleManager.Update 驱动）。
    /// </summary>
    internal sealed class BundleDownloadTask : IUpdateTask
    {
        private bool finished;
        private bool success;

        public float Progress { get; private set; }

        /// <summary>下载器当前阶段（None / Loading / Convert / Idle）。</summary>
        public LoaderState CurrentLoaderState { get; private set; }

        /// <summary>
        /// 下载阶段是否结束：进入 Convert（下载全部完成，开始解压）或直接 Idle（无需解压）。
        /// 不能用 success/finished 判断——它们要等「转换全部结束」才会被回调。
        /// </summary>
        public bool IsDone
        {
            get
            {
                return this.CurrentLoaderState == LoaderState.Convert
                       || this.CurrentLoaderState == LoaderState.Idle;
            }
        }

        public bool IsSucceeded
        {
            get { return this.success; }
        }

        public string Error { get; private set; }

        public void Start(UpdateContext ctx)
        {
            AssetBundleDownloader downloader = AssetBundleDownloader.Instance;
            downloader.Init();

            GameContext gameContext = new GameContext { result = ctx.CheckResult };

            downloader.StartDownload(
                gameContext,
                ok =>
                {
                    // 这个回调要等「下载 + 解压/转换」全部结束才触发
                    // （需要转换的包会把 callback 转交给 ConvertTask），
                    // 所以它代表整段流程完成，而不是下载完成。
                    this.success = ok;
                    this.finished = true;
                    ctx.DownloadFinished = true;
                    ctx.DownloadSucceeded = ok;

                    if (!ok || ctx.CheckResult == null)
                    {
                        return;
                    }

                    // 下载成功后的持久化与清理保持原行为。
                    AssetBundleUpdateChecker.SaveLocalManifest(ctx.CheckResult.customManifest);
                    AssetBundleManager.Instance.SetAssetBundleItem(ctx.CheckResult.customManifest);
                    AssetBundleCacheCleaner.CleanObsoleteBundles(ctx.CheckResult.customManifest);
                });
        }

        public void Tick(UpdateContext ctx)
        {
            AssetBundleDownloader downloader = AssetBundleDownloader.Instance;
            this.Progress = downloader.GetTotalProgress();
            this.CurrentLoaderState = downloader.GetLoaderState();

            if (this.finished && !this.success)
            {
                this.Error = "下载失败";
            }
        }

        public void Abort()
        {
            // 下载器由 AssetBundleManager 持有，这里不做破坏性操作。
        }
    }

    /// <summary>失败提示面板：等玩家确认后结束进程。</summary>
    internal sealed class ErrorDialogTask : IUpdateTask
    {
        private AotUpdateListErrorView view;
        private bool exitChosen;

        public bool IsDone
        {
            get { return this.exitChosen; }
        }

        public bool IsSucceeded
        {
            get { return this.exitChosen; }
        }

        public string Error { get; private set; }

        public void Start(UpdateContext ctx)
        {
            AotBootUi.EnsureEventSystem();

            string message = string.IsNullOrEmpty(ctx.Error)
                ? "资源更新失败，请重启游戏后重试。"
                : "资源更新失败：" + ctx.Error + "\n请重启游戏后重试。";

            this.view = new AotUpdateListErrorView();
            if (!this.view.Open(message, () => this.exitChosen = true, null, ctx.Parent))
            {
                Debug.LogError("[UpdateFlow] 无法显示错误面板: " + message);
                this.exitChosen = true;
            }
        }

        public void Tick(UpdateContext ctx)
        {
        }

        public void Abort()
        {
            if (this.view != null)
            {
                this.view.Close();
                this.view = null;
            }
        }
    }
}
