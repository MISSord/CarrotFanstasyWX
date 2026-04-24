using System;

namespace CarrotFantasy
{
    public abstract class BaseBattleState : BaseState
    {
        public BaseStateMachine stateMachine;
        public BaseBattleState(BaseStateMachine bstateMachine, String btype) : base(btype)
        {
            stateMachine = bstateMachine;
        }
    }
}
