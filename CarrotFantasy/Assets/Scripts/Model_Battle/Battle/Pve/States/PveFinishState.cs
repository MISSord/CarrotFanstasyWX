using UnityEngine;

namespace CarrotFantasy
{
    public class PveFinishState : BaseBattleState
    {
        private BattleMonsterComponent monsterComponent;
        private BattleDataComponent dataComponent;

        public PveFinishState(BaseStateMachine bstateMachine, string btype = null) : base(bstateMachine, btype)
        {

        }

        public override void Init()
        {
            this.monsterComponent = (BattleMonsterComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.MonsterComponent);
            this.dataComponent = (BattleDataComponent)GameManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
        }

        public override void stateIn()
        {
            if (this.dataComponent.CarrotIsDead())
            {
                this.dataComponent.gameOverByCarrotDead();
            }
            else
            {
                if (!this.monsterComponent.isCanNewMonsterWaves()) //击杀全部怪物了
                {
                    this.dataComponent.gameOverByMonsterDead();
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
            this.monsterComponent = null;
            this.dataComponent = null;
            base.Dispose();
        }
    }
}
