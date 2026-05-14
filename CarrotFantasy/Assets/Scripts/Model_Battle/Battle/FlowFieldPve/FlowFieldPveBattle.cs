namespace CarrotFantasy
{
    /// <summary>
    /// 流场寻路 PVE 战斗（新模式），与经典 <see cref="PveBattle"/> 组件组合分离，便于独立演进。
    /// </summary>
    public class FlowFieldPveBattle : BaseBattle
    {
        public FlowFieldPveBattle() : base()
        {
        }

        public override void Init()
        {
            this.stateMachine = new PveStateMachine(this);
            this.AddComponent(new BattleDataComponent(this));
            this.AddComponent(new BattleSimpleHitTestComponent(this));
            this.AddComponent(new BattleMapComponent(this));
            this.AddComponent(new BattleItemComponent(this));
            this.AddComponent(new BattleTowerComponent(this));
            this.AddComponent(new BattleFlowFieldComponent(this));
            this.AddComponent(new BattleMonsterComponent(this));
            this.AddComponent(new BattleBulletComponent(this));
            this.AddComponent(new BattleInputComponent(this));
            this.AddComponent(new BattleSchedulerComponent(this));

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

        public override void ClearGameInfo()
        {
            base.ClearGameInfo();
            this.RemoveListener();
        }

        public override void InitComponent()
        {
            this.GetComponent(BattleComponentType.DataComponent).Init();
            this.GetComponent(BattleComponentType.HitTestComponent).Init();
            this.GetComponent(BattleComponentType.MapComponent).Init();
            this.GetComponent(BattleComponentType.ItemComponent).Init();
            this.GetComponent(BattleComponentType.TowerComponent).Init();
            this.GetComponent(BattleComponentType.FlowFieldComponent).Init();
            this.GetComponent(BattleComponentType.MonsterComponent).Init();
            this.GetComponent(BattleComponentType.BulletComponent).Init();
            this.GetComponent(BattleComponentType.InputComponent).Init();
            this.GetComponent(BattleComponentType.SchedulerComponent).Init();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
