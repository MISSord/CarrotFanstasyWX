using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class UnitTransformComponent : BaseUnitComponent
    {
        private Fix64 x, y, z;
        public Fix64 lastFrameX, lastFrameY, lastFrameZ;//这个是小于上面的两个值的
        // frame数据是经过全部战斗组件后遍历后再确认，确保数值的可靠

        public Fix64 faceDirection; //面向的角度 怪兽改这个
        public Fix64 scale; //大小 一般不用

        public Fix64 rotation; //炮台改这个

        public Dictionary<String, Fix64> birthParam { get; set; }

        public BattleEventDispatcher battleEventDispatcher;
        public HitTestShape_Circle bodyHitTestShape { get; private set; }

        public Fix64 bodyRadius;

        public UnitTransformComponent()
        {
            this.unitComponentType = UnitComponentType.TRANSFORM;
        }

        public override void Init()
        {
            base.Init();
            Fix64Vector2 birthPosition = this.unit.birthPosition;
            this.battleEventDispatcher = this.unit.eventDipatcher;
            this.birthParam = this.unit.birthParam;


            this.faceDirection = birthParam["faceDirection"]; //度数
            this.rotation = Fix64.Zero; // 0 - 1 貌似
            this.scale = birthParam["scale"];
            this.bodyHitTestShape = new HitTestShape_Circle(HitShapeType.CIRCLE, Fix64.Zero, Fix64.Zero, Fix64.Zero);
            Fix64 x = birthPosition.X; // + (birthParam["offsetX"] != null ? birthParam["offsetX"] : Fix64.Zero);
            Fix64 y = birthPosition.Y; // + (birthParam["offsetY"] != null ? birthParam["offsetY"] : Fix64.Zero);
            Fix64 z = Fix64.Zero;
            this.x = x; //定点数
            this.y = y; //定点数
            this.z = z; //定点数

            this.lastFrameX = this.x;
            this.lastFrameY = this.y;
            this.lastFrameZ = this.z;

        }

        public Fix64Vector2 GetLastPosition() //给防御塔用
        {
            return new Fix64Vector2(this.x, this.y);
        }

        public override void LateTick(Fix64 time)
        {
            this.lastFrameX = this.x;
            this.lastFrameY = this.y;
            this.lastFrameZ = this.z;
        }

        private void ResetBodyShape()
        {
            this.bodyHitTestShape.Reset(this.x, this.y, this.bodyRadius);
        }

        public Fix64 GetBodyRadius()
        {
            return this.bodyRadius;
        }

        public void SetBodyRadius(Fix64 bodyRadius)
        {
            this.bodyRadius = bodyRadius;
            this.ResetBodyShape();
        }

        public bool SetPosition(Fix64 x, Fix64 y, Fix64 z)
        {
            //定点数
            if (this.x == x && this.y == y && this.z == z)
            {
                return false;
            }

            this.x = x;
            this.y = y;
            this.z = z;
            this.bodyHitTestShape.Reset(this.x, this.y, this.bodyRadius);
            this.unit.eventDipatcher.DispatchEvent(UnitEvent.POSITION_CHANGE);
            return true;
        }

        public void GetPosition(out float x, out float y, out float z) //视图用
        {
            x = (float)this.x;
            y = (float)this.y;
            z = (float)this.z;
        }

        public void GetLastFramePosition(out Fix64 x, out Fix64 y, out Fix64 z) //这个用于移动过程
        {
            x = this.lastFrameX;
            y = this.lastFrameY;
            z = this.lastFrameZ;
        }

        public void SetFaceDirection(Fix64 faceDirection)
        {
            //定点数
            if (this.faceDirection == faceDirection)
            {
                return;
            }
            this.faceDirection = faceDirection;
            this.unit.eventDipatcher.DispatchEvent(UnitEvent.FACE_DIRECTION_CHANGE);
        }

        public override void OnTick(Fix64 deltaTime)
        {

        }
    }
}
