namespace CarrotFantasy
{
    public class PveFightingState : BaseBattleState
    {
        private BattleMonsterComponent monsterComponent;
        private BattleDataComponent dataComponent;

        public PveFightingState(BaseStateMachine bstateMachine, string btype = null) : base(bstateMachine, btype)
        {

        }

        public override void Init()
        {
            base.Init();
            this.monsterComponent = (BattleMonsterComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.MonsterComponent);
            this.dataComponent = (BattleDataComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
        }

        public override void StateIn()
        {
            base.StateIn();
        }

        public override string OnTick(Fix64 time)
        {
            if (this.dataComponent.CarrotIsDead())
            {
                return BattleStateType.END_GAME;
            }
            if (this.monsterComponent.CheckIsHaveAnyMonsterSurvive())
            {
                return BattleStateType.FIGHTINT;
            }
            else
            {
                if (this.monsterComponent.IsCanNewMonsterWaves()) //还有怪物需要生产
                {
                    return BattleStateType.PRE_FIGHTINT;
                }
                else
                {
                    return BattleStateType.END_GAME;
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            this.monsterComponent = null;
            this.dataComponent = null;
        }
    }
}
