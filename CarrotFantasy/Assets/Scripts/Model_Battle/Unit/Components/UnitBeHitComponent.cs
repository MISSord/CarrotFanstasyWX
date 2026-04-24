namespace CarrotFantasy
{
    public class UnitBeHitComponent : BaseUnitComponent
    {
        public CallBack<BattleUnit> beHitCallBack { get; private set; }

        public UnitBeHitComponent()
        {
            this.unitComponentType = UnitComponentType.BEHIT;
        }

        public void registerBeHitCallBack(CallBack<BattleUnit> beHitCallBack)
        {
            this.beHitCallBack = beHitCallBack;
        }

        public override void onTick(Fix64 deltaTime)
        {

        }
    }
}
