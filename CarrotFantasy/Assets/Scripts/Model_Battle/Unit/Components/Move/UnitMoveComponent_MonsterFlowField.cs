namespace CarrotFantasy
{
    /// <summary>流场引导移动（新模式）；与 <see cref="UnitMoveComponent_Monster"/> 折线移动分离。</summary>
    public class UnitMoveComponent_MonsterFlowField : BaseUnitComponent, IMonsterLocomotion
    {
        protected UnitTransformComponent unitTransform;
        public bool isReachCarrot { get; private set; }
        public Fix64 EndPointDistance { get; private set; }

        private Fix64 speed;
        private BattleFlowFieldComponent flowField;
        private BattleMapComponent mapComponent;
        private Fix64 cellSize;
        private int goalGx;
        private int goalGy;
        private Fix64 goalCenterX;
        private Fix64 goalCenterY;

        public UnitMoveComponent_MonsterFlowField()
        {
            this.unitComponentType = UnitComponentType.MOVE_MONSTER_FLOW_FIELD;
        }

        public override void Init()
        {
            this.unitTransform = (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM);
            this.isReachCarrot = false;
            this.speed = this.unit.birthParam["speed"] != null ? this.unit.birthParam["speed"] : new Fix64(3);
        }

        public void LoadFlowField(BattleFlowFieldComponent flow)
        {
            this.flowField = flow;
            this.mapComponent = (BattleMapComponent)this.unit.baseBattle.GetComponent(BattleComponentType.MapComponent);
            this.cellSize = new Fix64(BattleConfig.MAP_RATIO);
            this.goalGx = flow.GoalGridX;
            this.goalGy = flow.GoalGridY;
            Fix64Vector2 g = this.mapComponent.GetMapGridPosition(this.goalGx, this.goalGy);
            this.goalCenterX = g.X;
            this.goalCenterY = g.Y;

            int gx;
            int gy;
            this.WorldToGrid(this.unit.birthPosition.X, this.unit.birthPosition.Y, out gx, out gy);
            this.EndPointDistance = this.DistanceCostToFix64(this.flowField.GetDistanceCost(gx, gy));
        }

        public void ClearMovementState()
        {
            this.flowField = null;
            this.mapComponent = null;
            this.isReachCarrot = false;
        }

        private Fix64 DistanceCostToFix64(int cost)
        {
            if (cost >= int.MaxValue / 8)
            {
                return new Fix64(9999f);
            }

            return new Fix64(cost / 1000f);
        }

        private void WorldToGrid(Fix64 px, Fix64 py, out int gx, out int gy)
        {
            gx = (int)(float)Fix64.Round(px / this.cellSize);
            gy = (int)(float)Fix64.Round(py / this.cellSize);
            if (gx < 0)
            {
                gx = 0;
            }

            if (gy < 0)
            {
                gy = 0;
            }

            if (gx >= this.mapComponent.xColumn)
            {
                gx = this.mapComponent.xColumn - 1;
            }

            if (gy >= this.mapComponent.yRow)
            {
                gy = this.mapComponent.yRow - 1;
            }
        }

        private void ReachCarrot()
        {
            this.isReachCarrot = true;
            this.unit.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.CARROT_LIVE_REDUCE);
            this.unit.eventDipatcher.DispatchEvent<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, (BattleUnit_Monster)this.unit);
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.isReachCarrot)
            {
                return;
            }

            Fix64 x;
            Fix64 y;
            Fix64 z;
            this.unitTransform.GetLastFramePosition(out x, out y, out z);

            if (this.flowField == null || !this.flowField.IsBuilt)
            {
                Fix64 dxToGoal = this.goalCenterX - x;
                Fix64 dyToGoal = this.goalCenterY - y;
                Fix64 lenSqG = dxToGoal * dxToGoal + dyToGoal * dyToGoal;
                Fix64 lenG = Fix64.Sqrt(lenSqG);
                if (lenG <= this.cellSize / new Fix64(4f))
                {
                    this.ReachCarrot();
                    return;
                }

                if (lenG > Fix64.Zero)
                {
                    Fix64 stepG = this.speed * deltaTime;
                    if (stepG > lenG)
                    {
                        stepG = lenG;
                    }

                    x = x + dxToGoal * stepG / lenG;
                    y = y + dyToGoal * stepG / lenG;
                    this.unitTransform.SetPosition(x, y, z);
                }

                return;
            }

            int gx;
            int gy;
            this.WorldToGrid(x, y, out gx, out gy);

            int dc = this.flowField.GetDistanceCost(gx, gy);
            this.EndPointDistance = this.DistanceCostToFix64(dc);

            Fix64 dxGoal = this.goalCenterX - x;
            Fix64 dyGoal = this.goalCenterY - y;
            Fix64 distGoalSq = dxGoal * dxGoal + dyGoal * dyGoal;
            Fix64 reachEps = this.cellSize / new Fix64(4f);
            if ((gx == this.goalGx && gy == this.goalGy) || distGoalSq <= reachEps * reachEps)
            {
                this.ReachCarrot();
                return;
            }

            int nx;
            int ny;
            this.flowField.GetNextGrid(gx, gy, out nx, out ny);

            Fix64 targetX;
            Fix64 targetY;
            if (nx < 0)
            {
                targetX = this.goalCenterX;
                targetY = this.goalCenterY;
            }
            else
            {
                Fix64Vector2 tc = this.mapComponent.GetMapGridPosition(nx, ny);
                targetX = tc.X;
                targetY = tc.Y;
            }

            Fix64 dx = targetX - x;
            Fix64 dy = targetY - y;
            Fix64 lenSq = dx * dx + dy * dy;
            Fix64 tiny = this.cellSize / new Fix64(100f);
            if (lenSq <= tiny * tiny)
            {
                this.unitTransform.SetPosition(targetX, targetY, z);
                return;
            }

            Fix64 len = Fix64.Sqrt(lenSq);
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
