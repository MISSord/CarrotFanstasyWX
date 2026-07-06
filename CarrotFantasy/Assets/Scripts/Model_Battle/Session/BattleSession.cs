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

    /// <summary>
    /// 单局战斗会话。同关重开：视图回池 → Model 重置 → 视图同步 → FinishRunningAfterViewReady。
    /// </summary>
    public sealed class BattleSession
    {
        readonly PveModelBattleParams launchParams;
        readonly BattleViewHost viewHost;

        BaseBattle battle;
        BattleView_base view;
        BattleSessionPhase phase = BattleSessionPhase.None;

        /// <summary>Restart/TearDown 时递增，异步预加载回调携带 token 校验，防止过期回调误建 View。</summary>
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

        /// <summary>进关入口：先同步初始化 Model，再启动异步视图流水线。</summary>
        public void Run()
        {
            if (this.disposed || this.launchParams == null)
            {
                BattleFlowLog.Abort("Run", "disposed=" + this.disposed + " launchParamsNull=" + (this.launchParams == null));
                return;
            }

            AudioClipPreloader.RunBattleDefaults(null);
            AudioManager.Instance.PlayMusicByResources("AudioClips/NormalMordel/BGMusic");

            this.SetupModel(); // 1/4 InitializingModel
            this.BeginViewPipeline(this.runToken); // 2/4 → 3/4 → 4/4
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

            int token = ++this.runToken;
            ViewManager.Instance?.CloseAllOpenViews();

            this.view.ResetForReplay(this.ResetModelForReplay);

            GameViewObjectPool.Instance.PrepareForReplay();

            if (!this.TryIsActiveRun(token, "Restart"))
            {
                return;
            }

            if (!this.CanReplayWithExistingView())
            {
                BattleFlowLog.Step("Restart", "视图未就绪，回退完整 Build 流水线");
                this.phase = BattleSessionPhase.LoadingAssets;
                this.BeginViewPipeline(token);
                return;
            }

            this.FinishRunningAfterViewReady(token, "Replay");
        }

        /// <summary>同关重开：内容已 Build、场景校验通过、关键 AB 仍 Warm。</summary>
        bool CanReplayWithExistingView()
        {
            return this.view.IsContentBuilt &&
                   this.view.ValidateSceneContent() &&
                   BattleViewAssetPreloader.IsWarm(this.battle);
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

        public void TearDown(bool destroyViewHierarchy)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.runToken++;
            this.RemoveListeners();

            BattleViewPrefabPreloader.Clear();
            BattleViewSpritePreloader.Clear();

            if (destroyViewHierarchy && this.view != null)
            {
                this.view.Dispose();
            }
            else if (this.view != null)
            {
                this.view.ShutdownContentOnly();
            }

            if (this.battle != null)
            {
                this.battle.Dispose();
            }

            this.view = null;
            this.battle = null;
            this.phase = BattleSessionPhase.Disposed;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.TearDown(true);
        }

        /// <summary>流程 1/4：按 Mode 创建战斗实例并 Init 全部 Model 组件。</summary>
        void SetupModel()
        {
            this.phase = BattleSessionPhase.InitializingModel;
            this.CreateBattle();
            this.AddListeners();
            this.InitBattleModel(resetExisting: false);
        }

        void ResetModelForReplay()
        {
            this.battle.ClearGameInfo();
            this.InitBattleModel(resetExisting: true);
        }

        void InitBattleModel(bool resetExisting)
        {
            this.ApplyRandomSession(resetExisting);
            this.battle.Init();
            this.battle.InitComponent();
        }

        /// <summary>流程 2/4：AB 预加载完成后回调 <see cref="BuildViewAndStart"/>。</summary>
        void BeginViewPipeline(int token)
        {
            if (!this.TryIsActiveRun(token, "BeginViewPipeline"))
            {
                return;
            }

            this.phase = BattleSessionPhase.LoadingAssets;

            BattleViewAssetPreloader.Run(this.battle, () => this.BuildViewAndStart(token));
        }

        /// <summary>流程 3/4 → 4/4：建 View、校验容器、开 NormalModelPanel，最后 StartGame。</summary>
        void BuildViewAndStart(int token)
        {
            if (!this.TryIsActiveRun(token, "BuildViewAndStart"))
            {
                return;
            }

            if (this.battle == null)
            {
                BattleFlowLog.Abort("BuildViewAndStart", "battle=null");
                return;
            }

            if (this.viewHost == null || !this.viewHost.IsSceneAlive())
            {
                BattleFlowLog.Abort("BuildViewAndStart", "BattleScene 场景壳已失效");
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
                BattleFlowLog.Abort("BuildViewAndStart", "Grid 预制体未预加载");
                return;
            }

            bool createdView = this.view == null;
            if (createdView)
            {
                this.view = new PveBattleView(this.battle, viewHost);
            }

            this.view.Init();

            if (!this.view.IsContentBuilt)
            {
                if (!this.view.BuildContentComponents())
                {
                    BattleFlowLog.Abort("BuildViewAndStart", "BuildContentComponents 返回 false");
                    return;
                }
            }

            if (!this.view.ValidateSceneContent())
            {
                BattleFlowLog.Abort("BuildViewAndStart", "战斗场景内容校验失败");
                return;
            }

            this.FinishRunningAfterViewReady(token, createdView ? "Build" : "Build");
        }

        bool FinishRunningAfterViewReady(int token, string pathTag)
        {
            if (!this.TryIsActiveRun(token, "FinishRunningAfterViewReady"))
            {
                return false;
            }

            BattleViewHost viewHost = this.RequireViewHost();
            if (viewHost == null)
            {
                return false;
            }

            if (!BattleViewOpener.Open<NormalModelPanel>(this.battle))
            {
                BattleFlowLog.Abort("FinishRunningAfterViewReady", "Open NormalModelPanel 失败");
                return false;
            }

            this.phase = BattleSessionPhase.Running;
            BattleScenePresentation.ConfigureMainCameraForBattle();
            this.battle.StartGame();
            this.view.StartGame();

            BattleFlowLog.Step(
                "4/4 Running (" + pathTag + ")",
                "containers=" + viewHost.GetSceneContainerChildCount() +
                " grids=" + viewHost.GetContainerChildCount("GridContainer"));
            return true;
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
