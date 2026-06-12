using System;

namespace CarrotFantasy
{
    public abstract class BaseBattleState : BaseState
    {
        public BaseStateMachine stateMachine;

        /// <summary>当前战斗内核引用；由状态机构造注入，不通过全局单例访问。</summary>
        protected BaseBattle Battle => (stateMachine as BaseStateMachine)?.Battle;

        public BaseBattleState(BaseStateMachine bstateMachine, String btype) : base(btype)
        {
            stateMachine = bstateMachine;
        }
    }
}
