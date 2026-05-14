using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>使用流场寻路的新模式怪物；位移由 <see cref="UnitMoveComponent_MonsterFlowField"/> 负责。</summary>
    public class BattleUnit_MonsterFlow : BattleUnit_Monster
    {
        public BattleUnit_MonsterFlow(BaseBattle battle) : base(battle)
        {
        }

        protected override void InstallLocomotion()
        {
            UnitMoveComponent_MonsterFlowField m = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_MonsterFlowField>(UnitComponentType.MOVE_MONSTER_FLOW_FIELD);
            if (m == null)
            {
                m = new UnitMoveComponent_MonsterFlowField();
            }

            this.locomotionComponent = m;
        }

        /// <summary>在 <see cref="BattleUnit.Init"/> 之后、<see cref="BattleUnit.InitComponents"/> 之前调用。</summary>
        public void LoadFlowMovement(BattleFlowFieldComponent flow)
        {
            ((UnitMoveComponent_MonsterFlowField)this.locomotionComponent).LoadFlowField(flow);
        }

        public override void LoadInfo3(List<Fix64Vector2> monsterPath, Fix64 distance)
        {
        }
    }
}
