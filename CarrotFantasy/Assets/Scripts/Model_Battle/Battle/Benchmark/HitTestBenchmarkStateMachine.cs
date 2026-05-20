namespace CarrotFantasy
{
    public class HitTestBenchmarkStateMachine : BaseStateMachine
    {
        public HitTestBenchmarkStateMachine(BaseBattle battle) : base(battle)
        {
        }

        protected override BaseBattleState CreateStateInstance(string type)
        {
            if (type.Equals(BattleStateType.START_GAME))
            {
                return new HitTestBenchmarkStartState(this, BattleStateType.START_GAME);
            }

            if (type.Equals(BattleStateType.FIGHTINT))
            {
                return new HitTestBenchmarkFightingState(this, BattleStateType.FIGHTINT);
            }

            return null;
        }
    }
}
