namespace CarrotFantasy
{
    /// <summary>基准测试战斗持续处于战斗态，不因波次清空而结束。</summary>
    public class HitTestBenchmarkFightingState : BaseBattleState
    {
        public HitTestBenchmarkFightingState(BaseStateMachine bstateMachine, string btype) : base(bstateMachine, btype)
        {
        }

        public override string OnTick(Fix64 time)
        {
            return BattleStateType.FIGHTINT;
        }
    }
}
