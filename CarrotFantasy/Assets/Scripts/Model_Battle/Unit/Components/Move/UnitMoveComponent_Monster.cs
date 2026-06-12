using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>沿关卡折线路径移动的怪物位移组件。</summary>
    public class UnitMoveComponent_Monster : BaseUnitComponent, IMonsterLocomotion
    {
        private List<Fix64Vector2> monsterPointList;
        protected UnitTransformComponent unitTransform;
        public bool isReachCarrot { get; private set; }
        public Fix64 EndPointDistance { get; private set; }

        private int roadPointIndex;

        private Fix64 speed;

        private Fix64 moveTotalTime = Fix64.Zero;
        private Fix64 moveCurTime = Fix64.Zero;

        private Fix64 speedX;
        private Fix64 speedY;

        public UnitMoveComponent_Monster()
        {
            this.unitComponentType = UnitComponentType.MOVE_MONSTER;
        }

        public override void Init()
        {
            this.unitTransform = (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM);
            this.roadPointIndex = 0;

            this.isReachCarrot = false;
            this.speed = this.unit.birthParam["speed"] != null ? this.unit.birthParam["speed"] : new Fix64(3);
        }

        public void LoadInfo(List<Fix64Vector2> monsterPath, Fix64 distance)
        {
            this.monsterPointList = monsterPath;
            this.EndPointDistance = distance;
            this.EnsureSpeedInitialized();
            if (this.monsterPointList != null && this.monsterPointList.Count >= 2)
            {
                this.SetSpeed();
            }
        }

        public void ClearMovementState()
        {
            this.monsterPointList = null;
            this.isReachCarrot = false;
            this.roadPointIndex = 0;
            this.moveCurTime = Fix64.Zero;
            this.moveTotalTime = Fix64.Zero;
        }

        void EnsureSpeedInitialized()
        {
            if (this.speed > Fix64.Zero)
            {
                return;
            }

            Fix64 configuredSpeed;
            if (this.unit != null &&
                this.unit.birthParam != null &&
                this.unit.birthParam.TryGetValue("speed", out configuredSpeed) &&
                configuredSpeed > Fix64.Zero)
            {
                this.speed = configuredSpeed;
                return;
            }

            this.speed = new Fix64(3);
        }

        private void SetSpeed()
        {
            this.EnsureSpeedInitialized();

            while (this.roadPointIndex < this.monsterPointList.Count - 1)
            {
                Fix64 dicX = this.monsterPointList[this.roadPointIndex + 1].X - this.monsterPointList[this.roadPointIndex].X;
                Fix64 dicY = this.monsterPointList[this.roadPointIndex + 1].Y - this.monsterPointList[this.roadPointIndex].Y;

                if (Fix64.Abs(dicX) > Fix64.Zero || Fix64.Abs(dicY) > Fix64.Zero)
                {
                    break;
                }

                this.roadPointIndex++;
            }

            if (this.roadPointIndex >= this.monsterPointList.Count - 1)
            {
                this.moveTotalTime = Fix64.Zero;
                this.speedX = Fix64.Zero;
                this.speedY = Fix64.Zero;
                this.moveCurTime = Fix64.Zero;
                return;
            }

            Fix64 dicXSegment = this.monsterPointList[this.roadPointIndex + 1].X - this.monsterPointList[this.roadPointIndex].X;
            Fix64 dicYSegment = this.monsterPointList[this.roadPointIndex + 1].Y - this.monsterPointList[this.roadPointIndex].Y;

            Fix64 moveXTime = Fix64.Abs(dicXSegment) / this.speed;
            Fix64 moveYTime = Fix64.Abs(dicYSegment) / this.speed;

            this.moveTotalTime = moveXTime >= moveYTime ? moveXTime : moveYTime;
            if (this.moveTotalTime <= Fix64.Zero)
            {
                this.moveTotalTime = new Fix64(0.001f);
            }

            Fix64 x;
            Fix64 y;
            Fix64 z;
            this.unitTransform.GetLastFramePosition(out x, out y, out z);

            Fix64 dicXmove = this.monsterPointList[this.roadPointIndex + 1].X - x;
            Fix64 dicYmove = this.monsterPointList[this.roadPointIndex + 1].Y - y;

            this.speedX = dicXmove / this.moveTotalTime;
            this.speedY = dicYmove / this.moveTotalTime;

            this.moveCurTime = Fix64.Zero;
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

            if (this.monsterPointList == null || this.monsterPointList.Count < 2)
            {
                return;
            }

            Fix64 speedMul = MonsterLocomotionUtil.GetSpeedMultiplier(this.unit);
            Fix64 scaledDelta = deltaTime * speedMul;

            Fix64 x;
            Fix64 y;
            Fix64 z;
            this.unitTransform.GetLastFramePosition(out x, out y, out z);
            Fix64 useDelta = scaledDelta;

            if (this.moveCurTime + useDelta >= this.moveTotalTime)
            {
                useDelta = this.moveTotalTime - this.moveCurTime;
            }

            x += useDelta * this.speedX;
            y += useDelta * this.speedY;

            this.moveCurTime += useDelta;
            Fix64 spd = this.speed;
            if (spd >= Fix64.Zero)
            {
                this.EndPointDistance -= spd * speedMul;
            }
            else
            {
                this.EndPointDistance += spd * speedMul;
            }

            this.unitTransform.SetPosition(x, y, z);
            if (this.moveCurTime >= this.moveTotalTime)
            {
                if (this.roadPointIndex + 1 < this.monsterPointList.Count)
                {
                    Fix64 xOffset = this.monsterPointList[this.roadPointIndex].X - this.monsterPointList[this.roadPointIndex + 1].X;
                    if (xOffset < Fix64.Zero)
                    {
                        this.unitTransform.SetFaceDirection(Fix64.Zero);
                    }
                    else if (xOffset > Fix64.Zero)
                    {
                        this.unitTransform.SetFaceDirection(new Fix64(180));
                    }
                }

                this.roadPointIndex++;
                if (this.roadPointIndex >= this.monsterPointList.Count - 1)
                {
                    this.ReachCarrot();
                    return;
                }

                this.SetSpeed();
            }
        }
    }
}
