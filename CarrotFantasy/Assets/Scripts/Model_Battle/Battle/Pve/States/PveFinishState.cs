using UnityEngine;

namespace CarrotFantasy
{
    public class PveFinishState : BaseBattleState
    {
        private IBattlePVEWaveMonster waveMonster;
        private BattlePVEDataComponent dataComponent;

        public PveFinishState(BaseStateMachine bstateMachine, string btype = null) : base(bstateMachine, btype)
        {

        }

        public override void Init()
        {
            this.waveMonster = BattlePVEWaveMonster.GetFrom(this.Battle);
            this.dataComponent = BattlePVEDataComponent.GetFrom(this.Battle);
        }

        public override void StateIn()
        {
            if (this.dataComponent.CarrotIsDead())
            {
                this.dataComponent.GameOverByCarrotDead();
            }
            else
            {
                if (!this.waveMonster.IsCanNewMonsterWaves()) //击杀全部怪物了
                {
                    this.dataComponent.GameOverByMonsterDead();
                }
                else
                {
                    Debug.Log("结算状态出现错误");
                    return;
                }
            }
        }

        public override string OnTick(Fix64 time)
        {
            return BattleStateType.END_GAME;
        }

        public override void Dispose()
        {
            this.waveMonster = null;
            this.dataComponent = null;
            base.Dispose();
        }
    }
}
