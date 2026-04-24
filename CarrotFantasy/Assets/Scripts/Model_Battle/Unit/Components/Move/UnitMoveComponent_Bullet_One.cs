namespace CarrotFantasy
{
    public class UnitMoveComponent_Bullet_One : UnitMoveComponent_Bullet
    {

        public UnitMoveComponent_Bullet_One() : base()
        {
            this.unitComponentType = UnitComponentType.MOVE_BULLET_ONE;
        }

        public override void onTick(Fix64 deltaTime)
        {
            this.calcuMoveSpeed();
            base.onTick(deltaTime);
        }
    }
}
