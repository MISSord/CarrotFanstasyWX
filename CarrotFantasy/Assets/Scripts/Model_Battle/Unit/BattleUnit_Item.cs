using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleUnit_Item : BattleUnit
    {
        public int curLive; //物体血量
        public int totalLive; //物体总血量

        private bool isHaveDead = false;

        public int itemId { get; private set; }
        public UnitTransformComponent tranComponent { get; private set; }
        public UnitBeHitComponent beHitComponent { get; private set; }

        private List<int> haveBeHit;

        public BattleUnit_Item(BaseBattle battle) : base(battle)
        {
            this.haveBeHit = new List<int>();
            this.unitType = BattleUnitType.ITEM;
        }

        public override void Init()
        {
            this.tranComponent = GameObjectPool.Instance.getNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.tranComponent == null)
            {
                this.tranComponent = new UnitTransformComponent();
            }
            this.beHitComponent = GameObjectPool.Instance.getNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (this.beHitComponent == null)
            {
                this.beHitComponent = new UnitBeHitComponent();
            }
            this.AddComponent(this.tranComponent);
            this.AddComponent(this.beHitComponent);

            this.beHitComponent.registerBeHitCallBack(this.beHitCallBack);
        }

        public void LoadInfo(int uid, Dictionary<string, Fix64> param, Fix64Vector2 birthPosition, int id)
        {
            this.LoadInfo(uid, param, birthPosition);
            this.curLive = (int)this.birthParam["live"];
            this.totalLive = (int)this.birthParam["live"];
            this.itemId = id;
        }

        public void loadInfo1()
        {
            this.tranComponent.setBodyRadius(this.birthParam["bodyRadius"]);
        }

        public override void OnTick(Fix64 deltaTime)
        {

        }

        public void beHitCallBack(BattleUnit battleUnit)
        {
            if (this.isHaveDead == true) return;
            if (battleUnit.unitType.Equals(BattleUnitType.BULLET))
            {
                BattleUnit_Bullet bullet = (BattleUnit_Bullet)battleUnit;
                if (this.haveBeHit.Contains(bullet.uid))
                {
                    return;
                }
                this.haveBeHit.Add(bullet.uid);
                this.curLive -= bullet.damage;
                this.eventDipatcher.DispatchEvent(BattleEvent.ITEM_LIVE_REDUCE);
                if (this.curLive <= 0)
                {
                    this.isHaveDead = true;
                    this.eventDipatcher.DispatchEvent(BattleEvent.ITEM_DIED, this);
                    return;
                }
            }
        }

        public bool isDead()
        {
            if (this.curLive <= 0) return true;
            return false;
        }

        public override void Dispose()
        {
            this.haveBeHit.Clear();
            base.Dispose();
        }
    }
}
