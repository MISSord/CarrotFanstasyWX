namespace CarrotFantasy
{
    /// <summary>朝固定世界坐标直线移动，到达后标记 <see cref="IsReached"/>。</summary>
    public class UnitMoveComponent_GridStraight : BaseUnitComponent, IMonsterLocomotion
    {
        private UnitTransformComponent unitTransform;
        private Fix64Vector2 targetPosition;
        private Fix64 speed;
        private Fix64 reachEpsilon;

        public bool IsReached { get; private set; }

        public bool isReachCarrot
        {
            get { return this.IsReached; }
        }

        public Fix64 EndPointDistance { get; private set; }

        public UnitMoveComponent_GridStraight()
        {
            this.unitComponentType = UnitComponentType.MOVE_GRID_STRAIGHT;
        }

        public override void Init()
        {
            this.unitTransform = (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM);
            this.speed = new Fix64(3);
            if (this.unit.birthParam != null)
            {
                if (this.unit.birthParam.ContainsKey("moveSpeed"))
                {
                    this.speed = this.unit.birthParam["moveSpeed"];
                }
                else if (this.unit.birthParam.ContainsKey("speed"))
                {
                    this.speed = this.unit.birthParam["speed"];
                }
            }

            this.reachEpsilon = new Fix64(BattleConfig.MAP_RATIO / 4f);
            this.IsReached = false;
        }

        public void LoadTarget(Fix64Vector2 worldTarget)
        {
            this.targetPosition = worldTarget;
            this.IsReached = false;
            this.RefreshRemainingDistance();
        }

        public void ClearMovementState()
        {
            this.IsReached = false;
            this.EndPointDistance = Fix64.Zero;
        }

        private void RefreshRemainingDistance()
        {
            Fix64 x;
            Fix64 y;
            Fix64 z;
            this.unitTransform.GetLastFramePosition(out x, out y, out z);
            Fix64 dx = this.targetPosition.X - x;
            Fix64 dy = this.targetPosition.Y - y;
            this.EndPointDistance = Fix64.Sqrt(dx * dx + dy * dy);
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.IsReached)
            {
                return;
            }

            Fix64 x;
            Fix64 y;
            Fix64 z;
            this.unitTransform.GetLastFramePosition(out x, out y, out z);

            Fix64 dx = this.targetPosition.X - x;
            Fix64 dy = this.targetPosition.Y - y;
            Fix64 lenSq = dx * dx + dy * dy;
            Fix64 len = Fix64.Sqrt(lenSq);
            this.EndPointDistance = len;

            if (len <= this.reachEpsilon)
            {
                this.unitTransform.SetPosition(this.targetPosition.X, this.targetPosition.Y, z);
                this.IsReached = true;
                this.EndPointDistance = Fix64.Zero;
                return;
            }

            Fix64 step = this.speed * deltaTime;
            if (step > len)
            {
                step = len;
            }

            x = x + dx * step / len;
            y = y + dy * step / len;

            if (dx > Fix64.Zero)
            {
                this.unitTransform.SetFaceDirection(Fix64.Zero);
            }
            else if (dx < Fix64.Zero)
            {
                this.unitTransform.SetFaceDirection(new Fix64(180));
            }

            this.unitTransform.SetPosition(x, y, z);
        }
    }
}
