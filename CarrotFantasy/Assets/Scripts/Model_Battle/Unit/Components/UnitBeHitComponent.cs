namespace CarrotFantasy
{
    public class UnitBeHitComponent : BaseUnitComponent
    {
        public CallBack<BattleUnit> BeHitCallBack { get; private set; }

        public UnitBeHitComponent()
        {
            this.unitComponentType = UnitComponentType.BEHIT;
        }

        public void RegisterBeHitCallBack(CallBack<BattleUnit> BeHitCallBack)
        {
            this.BeHitCallBack = BeHitCallBack;
        }

        public override void OnTick(Fix64 deltaTime)
        {

        }
    }
}
