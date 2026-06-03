namespace CarrotFantasy
{
    /// <summary>
    /// 进战斗时从 <see cref="RoguelikeRunServer"/> 读取背包，应用局内起始金币等加成。
    /// </summary>
    public class BattleRoguelikeRunBridgeComponent : BaseBattleComponent
    {
        public BattleRoguelikeRunBridgeComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.RoguelikeRunBridgeComponent;
        }

        public override void Init()
        {
        }

        public override void Start()
        {
            RoguelikeBattleModifiers.ApplyFromRun();
            if (!RoguelikeRunServer.Instance.IsRunActive)
            {
                return;
            }

            RoguelikeRunServer.Instance.CollectBattleModifiers(out int startCoinBonus, out _);
            if (startCoinBonus > 0)
            {
                this.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, startCoinBonus);
            }
        }

        public override void ClearInfo()
        {
            RoguelikeBattleModifiers.Clear();
            base.ClearInfo();
        }

        public override void Dispose()
        {
            RoguelikeBattleModifiers.Clear();
            base.Dispose();
        }
    }
}
