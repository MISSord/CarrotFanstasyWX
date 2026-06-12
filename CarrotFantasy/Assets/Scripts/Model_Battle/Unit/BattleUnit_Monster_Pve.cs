using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 经典 PVE 怪物：沿关卡 <see cref="BattlePVEMapComponent.monsterPathList"/> 折线移动。
    /// </summary>
    public class BattleUnit_Monster_Pve : BattleUnit_Monster
    {
        public BattleUnit_Monster_Pve(BaseBattle battle) : base(battle)
        {
        }

        protected override void InstallLocomotion()
        {
            UnitMoveComponent_Monster m = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_Monster>(UnitComponentType.MOVE_MONSTER);
            if (m == null)
            {
                m = new UnitMoveComponent_Monster();
            }

            this.locomotionComponent = m;
        }

        /// <summary>在 <see cref="Init"/> 与 <see cref="InitComponents"/> 之后调用，确保移动速度已初始化。</summary>
        public void LoadPathMovement(List<Fix64Vector2> monsterPath, Fix64 distance)
        {
            ((UnitMoveComponent_Monster)this.locomotionComponent).LoadInfo(monsterPath, distance);
        }
    }
}
