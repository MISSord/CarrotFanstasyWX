namespace CarrotFantasy
{
    /// <summary>
    /// 碰撞性能对比用 PVE：与 <see cref="PveBattle"/> 相同玩法组件，仅碰撞实现可在网格版 / 暴力版间切换。
    /// </summary>
    public class HitTestBenchmarkBattle : BaseBattle
    {
        bool componentsRegistered;

        public bool UseSpatialGridHitTest { get; private set; }

        public HitTestBenchmarkBattle() : base()
        {
        }

        public override void RegisterComponents()
        {
            if (this.componentsRegistered)
            {
                return;
            }

            this.UseSpatialGridHitTest = BattleParamServer.Instance != null
                && BattleParamServer.Instance.hitTestBenchmarkUseSpatialGrid;

            this.stateMachine = new HitTestBenchmarkStateMachine(this);
            this.AddComponent(new BattleTestDataComponent(this));

            if (this.UseSpatialGridHitTest)
            {
                this.AddComponent(new BattleSimpleHitTestComponent(this));
            }
            else
            {
                this.AddComponent(new BattleBruteForceHitTestComponent(this));
            }

            this.AddComponent(new BattleTestMapComponent(this));
            this.AddComponent(new BattleTestTowerComponent(this));
            this.AddComponent(new BattleTestUnitSpawnComponent(this));
            this.AddComponent(new BattleSchedulerComponent(this));
            this.AddComponent(new BattleHitTestBenchmarkStatsComponent(this));
            this.componentsRegistered = true;
        }

        public override void Init()
        {
            this.AddListener();
        }

        protected override void AddListener()
        {
            this.eventDispatcher.AddListener(BattleEvent.PAUSE_THE_GAME, this.PauseTheGame);
            this.eventDispatcher.AddListener(BattleEvent.GO_ON_GAME, this.GoOnTheGame);
        }

        protected override void RemoveListener()
        {
            this.eventDispatcher.RemoveListener(BattleEvent.PAUSE_THE_GAME, this.PauseTheGame);
            this.eventDispatcher.RemoveListener(BattleEvent.GO_ON_GAME, this.GoOnTheGame);
        }

        public override void ResetForNewRound()
        {
            this.RemoveListener();
            base.ResetForNewRound();
        }

        public override void InitComponent()
        {
            this.GetComponent(BattleComponentType.DataComponent).Init();
            this.GetComponent(BattleComponentType.MapComponent).Init();
            this.GetComponent(BattleComponentType.HitTestComponent).Init();
            this.GetComponent(BattleComponentType.TowerComponent).Init();
            this.GetComponent(BattleTestUnitSpawnComponent.ComponentTypeId).Init();
            this.GetComponent(BattleComponentType.SchedulerComponent).Init();
            this.GetComponent(BattleHitTestBenchmarkStatsComponent.ComponentTypeId).Init();
        }

        public override void Dispose()
        {
            BattleHitTestBenchmarkStatsComponent stats =
                this.GetComponent(BattleHitTestBenchmarkStatsComponent.ComponentTypeId) as BattleHitTestBenchmarkStatsComponent;
            if (stats != null)
            {
                stats.LogSummary(true);
            }

            base.Dispose();
        }
    }
}
