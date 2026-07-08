namespace CarrotFantasy
{
    public class PveStartState : BaseBattleState
    {
        private bool isTimeToPreFighting;
        private int introEndScheId;
        private int countdownScheId;

        public PveStartState(BaseStateMachine bstateMachine, string btype) : base(bstateMachine, btype)
        {
            this.isTimeToPreFighting = false;
        }

        public override void StateIn()
        {
            base.StateIn();
            BattleSchedulerComponent sch =
                this.Battle.GetComponent(BattleComponentType.SchedulerComponent) as BattleSchedulerComponent;
            if (sch == null)
            {
                UnityEngine.Debug.LogError("[PveStartState] SchedulerComponent 未就绪，跳过开场调度");
                return;
            }

            float introDelay = BattleParamAccess.GetEffectiveStartGameDelaySeconds(this.Battle);

            this.Battle.eventDispatcher.DispatchEvent(BattleEvent.START_GAME);
            this.countdownScheId = sch.DelayExeMultipleTimes(
                () => { this.Battle.eventDispatcher.DispatchEvent(BattleEvent.START_GAME_INTRO_COUNTDOWN); },
                1.0f);
            this.introEndScheId = sch.DelayExeOnceTimes(() =>
            {
                sch.SilenceSingleSche(this.countdownScheId);
                this.Battle.eventDispatcher.DispatchEvent(BattleEvent.START_GAME_INTRO_END);
                this.isTimeToPreFighting = true;
            }, introDelay);
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
            BattleSchedulerComponent sch =
                this.Battle.GetComponent(BattleComponentType.SchedulerComponent) as BattleSchedulerComponent;
            if (sch == null)
            {
                return;
            }

            sch.SilenceSingleSche(this.introEndScheId);
            sch.SilenceSingleSche(this.countdownScheId);
        }

        public override void Dispose()
        {
            base.Dispose();

        }

    }
}
