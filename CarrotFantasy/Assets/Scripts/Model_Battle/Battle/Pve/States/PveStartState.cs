namespace CarrotFantasy
{
    public class PveStartState : BaseBattleState
    {
        private bool isTimeToPreFighting;
        private int scheId;
        public PveStartState(BaseStateMachine bstateMachine, string btype) : base(bstateMachine, btype)
        {
            this.isTimeToPreFighting = false;
        }

        public override void StateIn()
        {
            base.StateIn();
            BattleSchedulerComponent sch = (BattleSchedulerComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            this.scheId = sch.DelayExeOnceTimes(() => { this.isTimeToPreFighting = true; }, 4.0f);
            GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.START_GAME);
        }

        public override string OnTick(Fix64 time)
        {
            if (this.isTimeToPreFighting == true)
            {
                return BattleStateType.PRE_FIGHTINT;
            }
            return BattleStateType.START_GAME;
        }

        public override void StateOut()
        {
            //双重保险
            BattleSchedulerComponent sch = (BattleSchedulerComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            sch.SilenceSingleSche(this.scheId);
        }

        public override void Dispose()
        {
            base.Dispose();

        }

    }
}
