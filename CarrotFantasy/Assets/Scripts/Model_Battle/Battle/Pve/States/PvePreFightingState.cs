using UnityEngine;

namespace CarrotFantasy
{
    public class PvePreFightingState : BaseBattleState
    {
        private bool isEnterFighting;

        private BattleMonsterComponent monsterComponent;
        private IBattlePVEWaveMonster waveMonster;
        private BattleSchedulerComponent scheComponent;
        private BattlePVEDataComponent dataComponent;

        public PvePreFightingState(BaseStateMachine bstateMachine, string btype) : base(bstateMachine, btype)
        {

        }

        public override void Init()
        {
            base.Init();
            this.monsterComponent = (BattleMonsterComponent)this.Battle.GetComponent(BattleComponentType.MonsterComponent);
            this.waveMonster = BattlePVEWaveMonster.GetFrom(this.Battle);
            this.scheComponent = (BattleSchedulerComponent)this.Battle.GetComponent(BattleComponentType.SchedulerComponent);
            this.dataComponent = BattlePVEDataComponent.GetFrom(this.Battle);
        }

        public override void StateIn()
        {
            if (this.monsterComponent.CheckIsHaveAnyMonsterSurvive() == true)
            {
                Debug.Log("进入战斗准备状态有问题");
                isEnterFighting = false;
                return;
            }
            this.dataComponent.WavesNumberChange();
            this.waveMonster.BuildNewWavesMonster();
            this.monsterComponent.scheId = scheComponent.DelayExeMultipleTimes(this.monsterComponent.RegisterNewMonster, 2.0f);
            this.isEnterFighting = true;
        }

        public override string OnTick(Fix64 time)
        {
            if (this.isEnterFighting == true)
            {
                return BattleStateType.FIGHTINT;
            }
            return BattleStateType.PRE_FIGHTINT;
        }

        public override void Dispose()
        {
            base.Dispose();
            this.monsterComponent = null;
            this.waveMonster = null;
            this.scheComponent = null;
            this.dataComponent = null;
        }
    }
}
