using UnityEngine;

namespace CarrotFantasy
{
    public enum BattleSessionPhase
    {
        None = 0,
        /// <summary>创建 PveBattle、注册组件、Init/InitComponent（流程 1/4）</summary>
        InitializingModel,
        /// <summary>异步预加载战斗 Prefab/Sprite（流程 2/4）</summary>
        LoadingAssets,
        /// <summary>构建 PveBattleView、格子容器、战斗 UI（流程 3/4）</summary>
        BuildingView,
        /// <summary>StartGame 已调用，Tick 生效（流程 4/4）</summary>
        Running,
        Disposed,
    }

    enum BattleRunIntent
    {
        Enter,
        Replay,
    }

    /// <summary>
    /// 单局战斗会话。进关与同关重开均走 <see cref="ExecutePipeline"/> 单一流水线。
    /// </summary>
    public sealed class BattleSession
    {
        readonly PveModelBattleParams launchParams;
        readonly BattleViewHost viewHost;
        readonly BattleAssetScope assetScope = new BattleAssetScope();

        BaseBattle battle;
        BattleView_base view;
        BattleSessionPhase phase = BattleSessionPhase.None;

        /// <summary>Restart/Shutdown 时递增，异步预加载回调携带 token 校验。</summary>
        int runToken;
        int battleRandomSeed;
        bool disposed;

        public BaseBattle Battle
        {
            get { return this.battle; }
        }

        public BattleView_base View
        {
            get { return this.view; }
        }

        public BattleSessionPhase Phase
        {
            get { return this.phase; }
        }

        public BattleSession(PveModelBattleParams launchParams, BattleViewHost viewHost)
        {
            this.launchParams = launchParams;
            this.viewHost = viewHost;
        }

        /// <summary>进关入口。</summary>
        public void Run()
        {
            if (this.disposed || this.launchParams == null)
            {
                BattleFlowLog.Abort("Run", "disposed=" + this.disposed + " launchParamsNull=" + (this.launchParams == null));
                return;
            }

            AudioClipPreloader.RunBattleDefaults(null);
            AudioManager.Instance.PlayBattleBgm();

            this.ExecutePipeline(BattleRunIntent.Enter, this.runToken);
        }

        public void Restart()
        {
            if (this.disposed || this.battle == null || this.view == null)
            {
                BattleFlowLog.Abort(
                    "Restart",
                    "disposed=" + this.disposed +
                    " battleNull=" + (this.battle == null) +
                    " viewNull=" + (this.view == null));
                return;
            }

            this.ExecutePipeline(BattleRunIntent.Replay, ++this.runToken);
        }

        /// <summary>进关 / 重开统一流水线：Prepare → EnsureAssets → EnsureView → EnterRunning。</summary>
        void ExecutePipeline(BattleRunIntent intent, int token)
        {
            if (!this.TryIsActiveRun(token, "ExecutePipeline"))
            {
                return;
            }

            if (intent == BattleRunIntent.Replay)
            {
                BattleViewOpener.CloseOverlayBattleViews();
                this.view.ResetRound(this.ResetModelForReplay);
                GameViewObjectPool.Instance.PrepareForReplay();

                if (!this.TryIsActiveRun(token, "ExecutePipeline/ReplayPrepare"))
                {
                    return;
                }
            }
            else
            {
                this.SetupModel();

                if (!this.TryIsActiveRun(token, "ExecutePipeline/SetupModel"))
                {
                    return;
                }
            }

            this.EnsureAssetsAndView(token, intent);
        }

        void EnsureAssetsAndView(int token, BattleRunIntent intent)
        {
            if (!this.TryIsActiveRun(token, "EnsureAssetsAndView"))
            {
                return;
            }

            this.phase = BattleSessionPhase.LoadingAssets;

            this.assetScope.EnsureLoaded(
                this.battle,
                onSuccess: () => this.BuildViewAndEnterRunning(token, intent),
                onFailure: () => this.HandlePipelineFailure(token, "预加载失败"));
        }

        void HandlePipelineFailure(int token, string reason)
        {
            if (!this.TryIsActiveRun(token, "HandlePipelineFailure"))
            {
                return;
            }

            BattleFlowLog.Abort("Pipeline", reason);
            UIServer.Instance?.ShowTip("战斗资源加载失败，请重试");
        }

        void BuildViewAndEnterRunning(int token, BattleRunIntent intent)
        {
            if (!this.TryIsActiveRun(token, "BuildViewAndEnterRunning"))
            {
                return;
            }

            if (this.battle == null)
            {
                BattleFlowLog.Abort("BuildViewAndEnterRunning", "battle=null");
                return;
            }

            if (this.viewHost == null || !this.viewHost.IsSceneAlive())
            {
                BattleFlowLog.Abort("BuildViewAndEnterRunning", "BattleScene 场景壳已失效");
                return;
            }

            BattleViewHost viewHost = this.RequireViewHost();
            if (viewHost == null)
            {
                return;
            }

            this.phase = BattleSessionPhase.BuildingView;

            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.Grid,
                out _))
            {
                this.HandlePipelineFailure(token, "Grid 预制体未预加载");
                return;
            }

            if (this.view == null)
            {
                this.view = new PveBattleView(this.battle, viewHost);
            }

            this.view.Init();

            if (!this.view.IsContentBuilt)
            {
                if (!this.view.Build())
                {
                    this.HandlePipelineFailure(token, "Build 返回 false");
                    return;
                }
            }

            if (!this.view.ValidateSceneContent())
            {
                this.HandlePipelineFailure(token, "战斗场景内容校验失败");
                return;
            }

            string pathTag = intent == BattleRunIntent.Replay ? "Replay" : "Build";
            this.EnterRunning(token, pathTag);
        }

        bool EnterRunning(int token, string pathTag)
        {
            if (!this.TryIsActiveRun(token, "EnterRunning"))
            {
                return false;
            }

            BattleViewHost viewHost = this.RequireViewHost();
            if (viewHost == null)
            {
                return false;
            }

            if (!BattleViewOpener.Open<NormalModelPanel>(this.battle, () => this.OnBattleMainPanelReady(token, pathTag)))
            {
                this.HandlePipelineFailure(token, "Open NormalModelPanel 失败");
                return false;
            }

            return true;
        }

        void OnBattleMainPanelReady(int token, string pathTag)
        {
            if (!this.TryIsActiveRun(token, "OnBattleMainPanelReady"))
            {
                return;
            }

            BattleViewHost viewHost = this.RequireViewHost();
            if (viewHost == null)
            {
                return;
            }

            this.phase = BattleSessionPhase.Running;
            BattleScenePresentation.ConfigureMainCameraForBattle();
            this.battle.StartGame();
            this.view.StartGame();

            BattleFlowLog.Step(
                "4/4 Running (" + pathTag + ")",
                "containers=" + viewHost.GetSceneContainerChildCount() +
                " grids=" + viewHost.GetContainerChildCount("GridContainer"));
        }

        public void Tick(float deltaSeconds)
        {
            if (this.disposed ||
                this.battle == null ||
                this.phase != BattleSessionPhase.Running)
            {
                return;
            }

            this.battle.Tick(new Fix64(deltaSeconds));
            if (this.view != null)
            {
                this.view.OnTick(deltaSeconds);
            }
        }

        /// <summary>离关统一 teardown（单链路）：战斗 UI → AB → 视图组件 → Model → 对象池。</summary>
        public void Shutdown()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.runToken++;
            this.RemoveListeners();

            AudioManager.Instance?.StopMusic();
            BattleViewEffectHelper.ClearActiveBuildEffects();
            BattleViewEffectHelper.ResetTemplates();

            BattleViewOpener.ReleaseAllBattleViews();

            this.assetScope.Release();

            if (this.view != null)
            {
                this.view.Dispose();
            }

            if (this.battle != null)
            {
                this.battle.Dispose();
            }

            this.view = null;
            this.battle = null;
            this.phase = BattleSessionPhase.Disposed;
        }

        /// <summary>流程 1/4：按 Mode 创建战斗实例并 Init 全部 Model 组件。</summary>
        void SetupModel()
        {
            this.phase = BattleSessionPhase.InitializingModel;
            this.CreateBattle();
            this.battle.RegisterComponents();
            this.AddListeners();
            this.InitBattleModel(resetExisting: false);
        }

        void ResetModelForReplay()
        {
            this.battle.ResetForNewRound();
            this.InitBattleModel(resetExisting: true);
        }

        void InitBattleModel(bool resetExisting)
        {
            this.ApplyRandomSession(resetExisting);
            this.battle.Init();
            this.battle.InitComponent();
        }

        /// <summary>按开战 Mode 选择战斗实现，并将本局参数注入 BaseBattle。</summary>
        void CreateBattle()
        {
            this.battle = CreatePveBattle(this.launchParams.Mode);
            this.battle.SetLaunchParams(this.launchParams);
            this.battle.SetHostBridge(new UnityBattleHostBridge());
        }

        BattleViewHost RequireViewHost()
        {
            if (this.viewHost == null || !this.viewHost.IsReady)
            {
                BattleFlowLog.Abort("RequireViewHost", "BattleViewHost 无效");
                return null;
            }

            return this.viewHost;
        }

        bool TryIsActiveRun(int token, string step)
        {
            if (this.disposed)
            {
                BattleFlowLog.Abort(step, "session 已 disposed，runToken=" + this.runToken + " callbackToken=" + token);
                return false;
            }

            if (token != this.runToken)
            {
                BattleFlowLog.Abort(
                    step,
                    "runToken 不匹配：current=" + this.runToken + " callback=" + token);
                return false;
            }

            return true;
        }

        void AddListeners()
        {
            this.battle.eventDispatcher.AddListener(BattleEvent.REPLAY_THE_GAME, this.OnReplayRequested);
            this.battle.eventDispatcher.AddListener<PveMatchSettlement>(
                BattleCoreEvent.PVE_MATCH_SETTLED,
                this.OnPveMatchSettled);
        }

        void RemoveListeners()
        {
            if (this.battle == null)
            {
                return;
            }

            this.battle.eventDispatcher.RemoveListener(BattleEvent.REPLAY_THE_GAME, this.OnReplayRequested);
            this.battle.eventDispatcher.RemoveListener<PveMatchSettlement>(
                BattleCoreEvent.PVE_MATCH_SETTLED,
                this.OnPveMatchSettled);
        }

        void OnReplayRequested()
        {
            this.Restart();
        }

        void OnPveMatchSettled(PveMatchSettlement settlement)
        {
            BattleSettlementPresenter.Handle(this.battle, settlement);
        }

        /// <summary>重开时保持 battleRandomSeed，仅 Reset 随机序列以保证可复现。</summary>
        void ApplyRandomSession(bool resetExisting)
        {
            if (this.battleRandomSeed == 0)
            {
                if (this.launchParams.BattleRandomSeed != 0)
                {
                    this.battleRandomSeed = this.launchParams.BattleRandomSeed;
                }
                else
                {
                    this.battleRandomSeed = DeterministicSeed.ForClassicLevel(
                        this.launchParams.BigLevelId,
                        this.launchParams.LevelId);
                }
            }

            if (this.battle.RandomSession == null ||
                this.battle.RandomSession.RootSeed != this.battleRandomSeed)
            {
                this.battle.SetRandomSession(new DeterministicRandomSession(this.battleRandomSeed));
            }
            else if (resetExisting)
            {
                this.battle.ResetRandomSession();
            }
        }

        static BaseBattle CreatePveBattle(BattlePveMode mode)
        {
            switch (mode)
            {
                case BattlePveMode.HitTestBenchmark:
                    return new HitTestBenchmarkBattle();
                case BattlePveMode.Roguelike:
                    return new RoguelikePveBattle();
                default:
                    return new PveBattle();
            }
        }
    }
}
