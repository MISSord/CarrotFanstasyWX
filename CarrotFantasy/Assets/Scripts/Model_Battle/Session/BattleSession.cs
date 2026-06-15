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
    /// 单局战斗会话，严格线性：模型初始化 → 资源预加载 → 视图构建 → 开战。
    /// </summary>
    public sealed class BattleSession
    {
        readonly PveModelBattleParams launchParams;
        readonly BattleSceneContext sceneContext;

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

        public BattleSession(PveModelBattleParams launchParams, BattleSceneContext context)
        {
            this.launchParams = launchParams;
            this.sceneContext = context;
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

            this.runToken++;
            ViewManager.Instance?.CloseAllOpenViews();
            this.ResetForReplay();
            this.BeginViewPipeline(this.runToken);
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

        void ResetForReplay()
        {
            this.view.TearDownSceneContainers();
            this.view.ClearGameInfo();
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

            if (this.sceneContext == null || !this.sceneContext.IsSceneAlive())
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
                this.view = new PveBattleView(this.battle, this.sceneContext.BattleRoot, viewHost);
            }

            this.view.Init();

            if (!this.view.InitContentComponents())
            {
                BattleFlowLog.Abort("BuildViewAndStart", "InitContentComponents 返回 false");
                return;
            }

            int containerCount = viewHost.GetSceneContainerChildCount();
            int gridCount = viewHost.GetContainerChildCount("GridContainer");
            if (containerCount < 6)
            {
                BattleFlowLog.Abort(
                    "BuildViewAndStart",
                    "SceneContainer 子容器=" + containerCount + "，期望 >=6");
                return;
            }

            if (gridCount <= 0)
            {
                BattleFlowLog.Abort(
                    "BuildViewAndStart",
                    "GridContainer 格子数=" + gridCount + "，期望 >0");
                return;
            }

            if (!BattleViewOpener.Open<NormalModelPanel>(this.battle))
            {
                BattleFlowLog.Abort("BuildViewAndStart", "Open NormalModelPanel 失败");
                return;
            }

            this.phase = BattleSessionPhase.Running;
            BattleScenePresentation.ConfigureMainCameraForBattle();
            this.battle.StartGame();
            this.view.StartGame();

            BattleFlowLog.Step(
                "4/4 Running",
                "containers=" + containerCount +
                " grids=" + gridCount);
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
            if (this.sceneContext == null || !this.sceneContext.IsValid)
            {
                BattleFlowLog.Abort("RequireViewHost", "BattleSceneContext 无效");
                return null;
            }

            return this.sceneContext.ViewHost;
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
