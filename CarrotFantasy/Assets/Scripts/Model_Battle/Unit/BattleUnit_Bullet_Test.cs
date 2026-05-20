using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 测试弹：随机格子出生，直线走向随机目标格，到达后移除；towerId=1，towerLevel=1。
    /// </summary>
    public class BattleUnit_Bullet_Test : BattleUnit_Bullet
    {
        private UnitMoveComponent_GridStraight gridMove;
        private bool removedAtTarget;

        public BattleUnit_Bullet_Test(BaseBattle battle) : base(battle)
        {
        }

        public void LoadGridMoveTarget(Fix64Vector2 worldTarget)
        {
            this.gridMove.LoadTarget(worldTarget);
        }

        public bool IsFinished()
        {
            return this.removedAtTarget || (this.gridMove != null && this.gridMove.IsReached);
        }

        public new void LoadInfo2(BattleUnit_Tower tower, BattleUnit target)
        {
            this.towerId = 1;
            this.towerLevel = 1;
        }

        public override void Init()
        {
            this.towerId = 1;
            this.towerLevel = 1;

            this.gridMove = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_GridStraight>(UnitComponentType.MOVE_GRID_STRAIGHT);
            if (this.gridMove == null)
            {
                this.gridMove = new UnitMoveComponent_GridStraight();
            }

            this.tranComponent = BattleUnitPool.Instance.GetNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.tranComponent == null)
            {
                this.tranComponent = new UnitTransformComponent();
            }

            this.beHitComponent = BattleUnitPool.Instance.GetNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (this.beHitComponent == null)
            {
                this.beHitComponent = new UnitBeHitComponent();
            }

            this.moveComponent = null;
            this.AddComponent(this.gridMove);
            this.AddComponent(this.tranComponent);
            this.AddComponent(this.beHitComponent);
            this.beHitComponent.RegisterBeHitCallBack(this.OnBeHit);
        }

        private void OnBeHit(BattleUnit unit)
        {
            if (unit.unitType.Equals(BattleUnitType.MONSTER) || unit.unitType.Equals(BattleUnitType.ITEM))
            {
                if (this.birthParam["isRemove"] == Fix64.Zero)
                {
                    this.eventDipatcher.DispatchEvent<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this);
                }
            }
        }

        public override void InitComponents()
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                this.componentList[i].Init();
            }

            this.tranComponent.SetBodyRadius(new Fix64(0.2f));
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.removedAtTarget)
            {
                return;
            }

            this.gridMove.OnTick(deltaTime);
            if (this.gridMove.IsReached)
            {
                this.removedAtTarget = true;
                this.eventDipatcher.DispatchEvent<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this);
            }
        }

        public override void ClearInfo()
        {
            this.removedAtTarget = false;
            this.gridMove = null;
            base.ClearInfo();
        }
    }
}
