namespace CarrotFantasy
{
    /// <summary>
    /// 追踪弹移动（<see cref="BulletMoveType.Homing"/>）。
    /// 打怪物：仅 HitTest 结算；打物品：移动内 TryResolveTargetHit + HitTest（去重）。见 BattleCombatFlow.md §5。
    /// </summary>
    public class UnitMoveComponent_Bullet : BaseUnitComponent
    {
        static readonly Fix64 MinHeadingDistance = new Fix64(0.01f);

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
            Fix64Vector2 curPosition = this.unitTran.GetLastPosition();

            Fix64Vector2 curDirect = targetPosition - curPosition;
            Fix64 longSide2 = curDirect.X * curDirect.X + curDirect.Y * curDirect.Y;
            if (longSide2 <= MinHeadingDistance * MinHeadingDistance)
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
            return this.UsesHomingHeading()
                   && this.unitTarget != null
                   && this.unitTarget.unitType.Equals(BattleUnitType.MONSTER);
        }

        /// <summary>追踪弹（配置 BulletMoveType.Homing）。</summary>
        protected virtual bool UsesHomingHeading()
        {
            return true;
        }

        /// <summary>仅追踪弹需要步长限制；直线弹保持匀速，命中交给碰撞/抵达判定。</summary>
        protected bool ShouldLimitStepTowardTarget()
        {
            if (!this.UsesHomingHeading())
            {
                return false;
            }

            return this.IsTargetHitValid() && this.unitTranTarget != null;
        }

        protected bool IsTargetHitValid()
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

        /// <summary>直线弹为 false，命中只走 HitTest。</summary>
        protected virtual bool UsesMoveResolveTargetHit()
        {
            return true;
        }

        /// <summary>本帧是否走移动内命中（仅追踪弹打物品时为 true）。</summary>
        protected bool ShouldTryResolveTargetHitNow()
        {
            if (!this.UsesMoveResolveTargetHit())
            {
                return false;
            }

            if (!this.IsTargetHitValid() || this.unitTran == null || this.unitTranTarget == null)
            {
                return false;
            }

            // 追踪弹对怪物每帧改向 + HitTest，移动内判定与 HitTest 重复，统一交给 HitTest。
            if (this.unitTarget.unitType.Equals(BattleUnitType.MONSTER))
            {
                return false;
            }

            return true;
        }

        /// <summary>与绑定开火目标的抵达判定；非穿透弹命中后返回 true 并跳过本帧剩余位移。</summary>
        protected bool TryResolveTargetHit()
        {
            if (!this.ShouldTryResolveTargetHitNow())
            {
                return false;
            }

            Fix64 cx = this.unitTran.GetLastPosition().X;
            Fix64 cy = this.unitTran.GetLastPosition().Y;
            Fix64 tx = this.unitTranTarget.GetLastPosition().X;
            Fix64 ty = this.unitTranTarget.GetLastPosition().Y;
            Fix64 sumR = this.unitTran.GetBodyRadius() + this.unitTranTarget.GetBodyRadius();
            Fix64 distSq = Battle_func.PGetDistanceSQ(cx, cy, tx, ty);
            if (distSq > sumR * sumR)
            {
                return false;
            }

            UnitBeHitComponent bulletBeHit = (UnitBeHitComponent)this.unit.GetComponent(UnitComponentType.BEHIT);
            UnitBeHitComponent targetBeHit = (UnitBeHitComponent)this.unitTarget.GetComponent(UnitComponentType.BEHIT);
            if (bulletBeHit != null && bulletBeHit.BeHitCallBack != null)
            {
                bulletBeHit.BeHitCallBack(this.unitTarget);
            }

            if (targetBeHit != null && targetBeHit.BeHitCallBack != null)
            {
                targetBeHit.BeHitCallBack(this.unit);
            }

            BattleUnit_Bullet bullet = (BattleUnit_Bullet)this.unit;
            return bullet.DestroyOnFirstHit();
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.TryResolveTargetHit())
            {
                return;
            }

            if (this.ShouldRecalculateHeading())
            {
                this.CalcuMoveSpeed();
            }

            Fix64 x, y, z;
            this.unitTran.GetLastFramePosition(out x, out y, out z);

            Fix64 dx = deltaTime * this.moveSpeedX;
            Fix64 dy = deltaTime * this.moveSpeedY;
            if (this.ShouldLimitStepTowardTarget())
            {
                Fix64 tx = this.unitTranTarget.GetLastPosition().X;
                Fix64 ty = this.unitTranTarget.GetLastPosition().Y;
                Fix64 distX = tx - x;
                Fix64 distY = ty - y;
                Fix64 distSq = distX * distX + distY * distY;
                Fix64 sumR = this.unitTran.GetBodyRadius() + this.unitTranTarget.GetBodyRadius();
                if (distSq > sumR * sumR)
                {
                    Fix64 dist = Fix64.Sqrt(distSq);
                    Fix64 maxStep = dist - sumR;
                    if (maxStep < Fix64.Zero)
                    {
                        maxStep = Fix64.Zero;
                    }

                    Fix64 stepLenSq = dx * dx + dy * dy;
                    if (stepLenSq > Fix64.Zero)
                    {
                        Fix64 stepLen = Fix64.Sqrt(stepLenSq);
                        if (stepLen > maxStep)
                        {
                            Fix64 scale = maxStep / stepLen;
                            dx *= scale;
                            dy *= scale;
                        }
                    }
                }
            }

            x += dx;
            y += dy;
            this.unitTran.SetPosition(x, y, z);

            if (this.TryResolveTargetHit())
            {
                return;
            }

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
