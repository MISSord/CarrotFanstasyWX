namespace CarrotFantasy
{
    public class UnitMoveComponent_Bullet_One : UnitMoveComponent_Bullet
    {

        public UnitMoveComponent_Bullet_One() : base()
        {
            this.unitComponentType = UnitComponentType.MOVE_BULLET_ONE;
        }

        public override void OnTick(Fix64 deltaTime)
        {
            this.CalcuMoveSpeed();
            base.OnTick(deltaTime);
        }
    }
}
