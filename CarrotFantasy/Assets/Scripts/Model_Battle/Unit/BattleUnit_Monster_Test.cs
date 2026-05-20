using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 测试怪：随机格子出生，直线走向随机目标格，到达后自我消亡（不扣萝卜血）。
    /// </summary>
    public class BattleUnit_Monster_Test : BattleUnit_Monster
    {
        private UnitMoveComponent_GridStraight gridMove;
        private bool diedAtTarget;

        public BattleUnit_Monster_Test(BaseBattle battle) : base(battle)
        {
        }

        /// <summary>在 <see cref="Init"/> 之后调用，设置直线移动目标。</summary>
        public void LoadGridMoveTarget(Fix64Vector2 worldTarget)
        {
            this.gridMove.LoadTarget(worldTarget);
        }

        public void MarkDiedAtTarget()
        {
            this.diedAtTarget = true;
        }

        protected override void InstallLocomotion()
        {
            this.gridMove = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_GridStraight>(UnitComponentType.MOVE_GRID_STRAIGHT);
            if (this.gridMove == null)
            {
                this.gridMove = new UnitMoveComponent_GridStraight();
            }

            this.locomotionComponent = this.gridMove;
        }

        public override bool IsDead()
        {
            if (this.diedAtTarget || (this.gridMove != null && this.gridMove.IsReached))
            {
                return true;
            }

            if (this.curLive <= 0)
            {
                return true;
            }

            return false;
        }

        public override void ClearInfo()
        {
            this.diedAtTarget = false;
            this.gridMove = null;
            base.ClearInfo();
        }
    }
}
