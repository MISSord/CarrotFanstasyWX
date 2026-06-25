namespace CarrotFantasy
{
    /// <summary>
    /// 追踪弹移动（<see cref="BulletMoveType.Homing"/>）。
    /// 仅按子弹与目标坐标差做改向与位移；命中由 HitTest 结算。见 BattleCombatFlow.md §4。
    /// </summary>
    public class UnitMoveComponent_Bullet : BaseUnitComponent
    {
        /// <summary>仅防归一化除零；不在此距离停弹，命中交给 HitTest。</summary>
        static readonly Fix64 MinDirectionLengthSq = new Fix64(0.000001f);

        protected Fix64 moveSpeed;

        public Fix64 moveSpeedX { get; protected set; }
        public Fix64 moveSpeedY { get; protected set; }


        protected Fix64Vector2 mapLeftBottomPosition;
        protected Fix64Vector2 mapRightTopPosition;

        protected UnitTransformComponent unitTran;

        protected BattleUnit unitTarget;
        protected UnitTransformComponent unitTranTarget;

        public UnitMoveComponent_Bullet()
        {
            this.unitComponentType = UnitComponentType.MOVE_BULLET;
        }

        public override void Init()
        {
            this.moveSpeed = ((BattleUnit_Bullet)unit).moveSpeed;
            this.unitTran = (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM);
            BattleMapComponent map = (BattleMapComponent)this.unit.baseBattle.GetComponent(BattleComponentType.MapComponent);
            this.mapLeftBottomPosition = map.mapLeftBottomPosition;
            this.mapRightTopPosition = map.mapRightTopPosition;
        }

        public virtual void RegisterMoveDirect(BattleUnit unit)
        {
            this.unitTarget = unit;
            this.unitTranTarget = (UnitTransformComponent)this.unitTarget.GetComponent(UnitComponentType.TRANSFORM);
            this.CalcuMoveSpeed();
        }

        public virtual void RemoveMoveDirect(BattleUnit unit)
        {
            if (unit == unitTarget)
            {
                this.unitTarget = null;
                this.unitTranTarget = null;
            }
        }

        public virtual void CalcuMoveSpeed()
        {
            if (this.unitTarget == null) return;
            if (this.unitTran == null) return;
            if (this.unitTranTarget == null) return;

            Fix64Vector2 targetPosition = this.unitTranTarget.GetLastPosition();
            Fix64 bx, by, bz;
            this.unitTran.GetLastFramePosition(out bx, out by, out bz);
            Fix64Vector2 curPosition = new Fix64Vector2(bx, by);

            Fix64Vector2 curDirect = targetPosition - curPosition;
            Fix64 longSide2 = curDirect.X * curDirect.X + curDirect.Y * curDirect.Y;
            if (longSide2 <= MinDirectionLengthSq)
            {
                this.moveSpeedX = Fix64.Zero;
                this.moveSpeedY = Fix64.Zero;
                return;
            }

            Fix64 longSide = Fix64.Sqrt(longSide2);
            Fix64 sinOne = curDirect.X / longSide;
            Fix64 cosOne = curDirect.Y / longSide;
            this.moveSpeedX = sinOne * this.moveSpeed;
            this.moveSpeedY = cosOne * this.moveSpeed;
        }

        protected bool ShouldRecalculateHeading()
        {
            if (!this.UsesHomingHeading() || !this.IsTargetValid() || this.unitTranTarget == null)
            {
                return false;
            }

            return this.unitTarget.unitType.Equals(BattleUnitType.MONSTER)
                   || this.unitTarget.unitType.Equals(BattleUnitType.ITEM);
        }

        /// <summary>追踪弹（配置 BulletMoveType.Homing）。</summary>
        protected virtual bool UsesHomingHeading()
        {
            return true;
        }

        protected bool IsTargetValid()
        {
            if (this.unitTarget == null)
            {
                return false;
            }

            if (this.unitTarget.unitType.Equals(BattleUnitType.MONSTER))
            {
                return !((BattleUnit_Monster)this.unitTarget).IsDamageImmune();
            }

            if (this.unitTarget.unitType.Equals(BattleUnitType.ITEM))
            {
                return !((BattleUnit_Item)this.unitTarget).IsDead();
            }

            return true;
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.ShouldRecalculateHeading())
            {
                this.CalcuMoveSpeed();
            }

            Fix64 x, y, z;
            this.unitTran.GetLastFramePosition(out x, out y, out z);

            x += deltaTime * this.moveSpeedX;
            y += deltaTime * this.moveSpeedY;
            this.unitTran.SetPosition(x, y, z);

            if (x <= (this.mapLeftBottomPosition.X) || x >= (this.mapRightTopPosition.X)
                || y <= (this.mapLeftBottomPosition.Y) || y >= (this.mapRightTopPosition.Y))
            {
                ((BattleUnit_Bullet)this.unit).RequestRemove();
            }
        }

        public override void Dispose()
        {
            this.moveSpeedX = Fix64.Zero;
            this.moveSpeedY = Fix64.Zero;
            this.unitTran = null;
            this.unitTranTarget = null;
            this.unitTarget = null;
            base.Dispose();
        }
    }
}
