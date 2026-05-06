namespace CarrotFantasy
{
    public class UnitMoveComponent_Bullet : BaseUnitComponent
    {
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
            if (unit == unitTarget) this.unitTarget = null;
        }

        public virtual void CalcuMoveSpeed()
        {
            if (this.unitTarget == null) return;
            if (this.unitTran == null) return;
            Fix64Vector2 targetPosition = new Fix64Vector2(this.unitTranTarget.lastFrameX, this.unitTranTarget.lastFrameY);
            Fix64Vector2 curPosition = new Fix64Vector2(this.unitTran.lastFrameX, this.unitTran.lastFrameY);

            Fix64Vector2 curDirect = targetPosition - curPosition;
            Fix64 longSide2 = curDirect.X * curDirect.X + curDirect.Y * curDirect.Y;
            Fix64 longSide = Fix64.Sqrt(longSide2);
            Fix64 sinOne = curDirect.X / longSide;
            Fix64 cosOne = curDirect.Y / longSide;
            this.moveSpeedX = sinOne * this.moveSpeed;
            this.moveSpeedY = cosOne * this.moveSpeed;
        }

        public override void OnTick(Fix64 deltaTime)
        {
            Fix64 x, y, z;
            this.unitTran.GetLastFramePosition(out x, out y, out z);
            x += deltaTime * this.moveSpeedX;
            y += deltaTime * this.moveSpeedY;
            this.unitTran.SetPosition(x, y, z);

            if (x <= (this.mapLeftBottomPosition.X) || x >= (this.mapRightTopPosition.X)
                || y <= (this.mapLeftBottomPosition.Y) || y >= (this.mapRightTopPosition.Y))
            {
                this.unit.eventDipatcher.DispatchEvent<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, (BattleUnit_Bullet)this.unit);
            }
        }

        public override void Dispose()
        {
            this.unitTran = null;
            this.unitTranTarget = null;
            this.unitTarget = null;
        }
    }
}
