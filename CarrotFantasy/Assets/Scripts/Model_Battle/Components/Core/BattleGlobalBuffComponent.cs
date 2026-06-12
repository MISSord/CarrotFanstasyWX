namespace CarrotFantasy
{
    /// <summary>
    /// 战斗级全局 Buff：开战时根据 <see cref="PveModelBattleParams"/> 编译快照，供金币、塔伤等系统读取。
    /// </summary>
    public class BattleGlobalBuffComponent : BaseBattleComponent
    {
        private BattleGlobalBuffSnapshot snapshot = new BattleGlobalBuffSnapshot();

        public BattleGlobalBuffComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.GlobalBuffComponent;
        }

        public static BattleGlobalBuffComponent GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.GlobalBuffComponent) as BattleGlobalBuffComponent;
        }

        public BattleGlobalBuffSnapshot Snapshot => this.snapshot;

        public Fix64 GetTowerDamageMultiplier()
        {
            return this.snapshot.GetTowerDamageMultiplier();
        }

        public override void Init()
        {
            PveModelBattleParams launchParams = BattleParamAccess.Current;
            this.snapshot = BattleGlobalBuffCompiler.Compile(launchParams);
        }

        public override void Start()
        {
            if (this.snapshot.StartCoinBonus > 0)
            {
                this.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, this.snapshot.StartCoinBonus);
            }
        }

        public override void ClearInfo()
        {
            this.snapshot = new BattleGlobalBuffSnapshot();
            base.ClearInfo();
        }
    }
}
