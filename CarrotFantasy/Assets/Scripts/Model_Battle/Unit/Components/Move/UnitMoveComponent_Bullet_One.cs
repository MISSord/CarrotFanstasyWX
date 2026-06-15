namespace CarrotFantasy
{
    /// <summary>
    /// 直线弹（<see cref="BulletMoveType.Straight"/>）。开火时锁定方向；命中仅由 HitTest 结算。
    /// </summary>
    public class UnitMoveComponent_Bullet_One : UnitMoveComponent_Bullet
    {
        bool headingLocked;

        public UnitMoveComponent_Bullet_One() : base()
        {
            this.unitComponentType = UnitComponentType.MOVE_BULLET_ONE;
        }

        public override void Init()
        {
            this.headingLocked = false;
            base.Init();
        }

        public override void RegisterMoveDirect(BattleUnit unit)
        {
            this.headingLocked = false;
            base.RegisterMoveDirect(unit);
            this.headingLocked = true;
        }

        public override void CalcuMoveSpeed()
        {
            if (this.headingLocked)
            {
                return;
            }

            base.CalcuMoveSpeed();
        }

        protected override bool UsesHomingHeading()
        {
            return false;
        }

        protected override bool UsesMoveResolveTargetHit()
        {
            return false;
        }

        public override void Dispose()
        {
            this.headingLocked = false;
            base.Dispose();
        }
    }
}
