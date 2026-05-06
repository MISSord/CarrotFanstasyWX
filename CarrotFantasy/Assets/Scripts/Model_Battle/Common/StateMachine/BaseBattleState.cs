using System;

namespace CarrotFantasy
{
    public abstract class BaseBattleState : BaseState
    {
        public BaseStateMachine stateMachine;

        /// <summary>当前战斗内核引用；替代 BattleManager.Instance.baseBattle。</summary>
        protected BaseBattle Battle => (stateMachine as BaseStateMachine)?.Battle;

        public BaseBattleState(BaseStateMachine bstateMachine, String btype) : base(btype)
        {
            stateMachine = bstateMachine;
        }
    }
}
