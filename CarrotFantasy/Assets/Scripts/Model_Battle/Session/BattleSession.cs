using UnityEngine;

namespace CarrotFantasy
{
    public enum BattleSessionPhase
    {
        None = 0,
        InitializingModel,
        LoadingAssets,
        BuildingView,
        Running,
        Disposed,
    }

    /// <summary>
    /// 单局战斗会话，严格线性：模型初始化 → 资源预加载 → 视图构建 → 开战。
    /// 场景壳引用只通过 <see cref="BattleSceneContext"/> 获取，不在 config 中缓存。
    /// </summary>
    public sealed class BattleSession
    {
        readonly BattleSessionConfig config;
        readonly BattleSceneContext sceneContext;
        readonly BattleSessionHost sessionHost;

        BaseBattle battle;
        BattleView_base view;
        BattleSessionPhase phase = BattleSessionPhase.None;
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

        public BattleSession(
            BattleSessionConfig sessionConfig,
            BattleSceneContext context,
            BattleSessionHost host)
        {
            this.config = sessionConfig;
            this.sceneContext = context;
            this.sessionHost = host;
        }

        public void Run()
        {
            if (this.disposed || this.config == null)
            {
                BattleFlowLog.Abort("Run", "disposed=" + this.disposed + " configNull=" + (this.config == null));
                return;
            }

            BattleFlowLog.Step(
                "Run 开始",
                "runToken=" + this.runToken +
                " root#" + (this.sceneContext != null && this.sceneContext.BattleRoot != null
                    ? this.sceneContext.BattleRoot.GetInstanceID().ToString()
                    : "null") +
                " level=" + this.config.Params.BigLevelId + "-" + this.config.Params.LevelId);

            AudioClipPreloader.RunBattleDefaults(null);
            AudioManager.Instance.PlayMusicByResources("AudioClips/NormalMordel/BGMusic");

            this.SetupModel();
            this.BeginViewPipeline(this.runToken);
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
            BattleFlowLog.Step("Restart", "runToken=" + this.runToken);
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
            BattleFlowLog.Step(
                "TearDown",
                "destroyViewHierarchy=" + destroyViewHierarchy +
                " phase=" + this.phase +
                " runToken=" + this.runToken);

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

        void SetupModel()
        {
            this.phase = BattleSessionPhase.InitializingModel;
            BattleFlowLog.Step("1/4 SetupModel", "phase=" + this.phase);

            this.CreateBattle();
            this.AddListeners();
            this.InitBattleModel(resetExisting: false);

            BattleFlowLog.Step(
                "1/4 SetupModel 完成",
                "battle=" + (this.battle != null ? this.battle.GetType().Name : "null") +
                " view=" + (this.view != null ? "已存在" : "null(预期)"));
        }

        void ResetForReplay()
        {
            BattleFlowLog.Step("ResetForReplay", "runToken=" + this.runToken);
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

        void BeginViewPipeline(int token)
        {
            if (!this.TryIsActiveRun(token, "BeginViewPipeline"))
            {
                return;
            }

            this.phase = BattleSessionPhase.LoadingAssets;
            BattleFlowLog.Step("2/4 BeginViewPipeline", "phase=" + this.phase + " runToken=" + token);

            BattleViewAssetPreloader.Run(this.battle, () => this.BuildViewAndStart(token));
        }

        void BuildViewAndStart(int token)
        {
            BattleFlowLog.Step("3/4 BuildViewAndStart 回调", "runToken=" + token + " phase=" + this.phase);

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

            BattleFlowLog.ViewHostSnapshot("BuildViewAndStart/ViewHost", viewHost);

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
                BattleFlowLog.Step(
                    "BuildViewAndStart 创建 View",
                    "root#" + this.sceneContext.BattleRoot.GetInstanceID() +
                    " ViewHost#" + viewHost.GetInstanceID());
            }
            else
            {
                BattleFlowLog.Step("BuildViewAndStart 复用 View", "hasComponents=" + this.view.HasRegisteredComponents);
            }

            this.view.Init();
            BattleFlowLog.Step(
                "BuildViewAndStart view.Init",
                "hasComponents=" + this.view.HasRegisteredComponents);

            if (!this.view.InitContentComponents())
            {
                BattleFlowLog.Abort("BuildViewAndStart", "InitContentComponents 返回 false");
                return;
            }

            BattleFlowLog.ViewHostSnapshot("BuildViewAndStart/InitContent 后", viewHost);

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
                "phase=" + this.phase +
                " runToken=" + token +
                " sceneChildren=" + containerCount +
                " gridChildren=" + gridCount +
                " battle.isStart=" + this.battle.isStart +
                " view.isStart=" + this.view.isStart);
        }

        void CreateBattle()
        {
            PveModelBattleParams launchParams = this.config.Params;
            this.battle = CreatePveBattle(launchParams.Mode);
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
            this.sessionHost.HandlePveMatchSettled(settlement);
        }

        void ApplyRandomSession(bool resetExisting)
        {
            if (this.battleRandomSeed == 0)
            {
                if (this.config.BattleRandomSeed != 0)
                {
                    this.battleRandomSeed = this.config.BattleRandomSeed;
                }
                else
                {
                    this.battleRandomSeed = DeterministicSeed.ForClassicLevel(
                        this.config.Params.BigLevelId,
                        this.config.Params.LevelId);
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
