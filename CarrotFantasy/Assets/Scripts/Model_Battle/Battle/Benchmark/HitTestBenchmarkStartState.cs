namespace CarrotFantasy
{
    public class HitTestBenchmarkStartState : BaseBattleState
    {
        public HitTestBenchmarkStartState(BaseStateMachine bstateMachine, string btype) : base(bstateMachine, btype)
        {
        }

        public override void StateIn()
        {
            base.StateIn();
            this.Battle.eventDispatcher.DispatchEvent(BattleEvent.START_GAME);
        }

        public override string OnTick(Fix64 time)
        {
            return BattleStateType.FIGHTINT;
        }
    }
}
