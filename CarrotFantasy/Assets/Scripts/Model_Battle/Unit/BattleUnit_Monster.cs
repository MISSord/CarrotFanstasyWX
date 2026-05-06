using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleUnit_Monster : BattleUnit
    {
        public int curLive; //怪物血量
        public int totalLive; //怪物总血量
        private UnitMoveComponent_Monster moveTrans;
        protected UnitTransformComponent unitTransform;

        private List<int> haveBeHit;
        private bool isHaveDead = false;

        public int monsterId { get; private set; }

        public int curLevel { get; private set; }

        public Fix64 EndPointDistance { get; private set; }

        public BattleUnit_Monster(BaseBattle battle) : base(battle)
        {
            this.unitType = BattleUnitType.MONSTER;
            this.haveBeHit = new List<int>();
        }

        public override void LoadInfo(int uid, Dictionary<string, Fix64> param, Fix64Vector2 birthPosition)
        {
            base.LoadInfo(uid, param, birthPosition);
            this.curLive = (int)this.birthParam["live"];
            this.totalLive = (int)this.birthParam["live"];
        }

        public void LoadInfo2(int curLevel, int monsterId)
        {
            this.curLevel = curLevel;
            this.monsterId = monsterId;
        }

        public void LoadInfo3(List<Fix64Vector2> monsterPath, Fix64 distance)
        {
            this.moveTrans.LoadInfo(monsterPath, distance);
        }

        public override void Init()
        {
            this.unitTransform = BattleUnitPool.Instance.GetNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.unitTransform == null)
            {
                this.unitTransform = new UnitTransformComponent();
            }
            this.moveTrans = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_Monster>(UnitComponentType.MOVE_MONSTER);
            if (this.moveTrans == null)
            {
                this.moveTrans = new UnitMoveComponent_Monster();
            }
            UnitBeHitComponent beHit = BattleUnitPool.Instance.GetNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (beHit == null)
            {
                beHit = new UnitBeHitComponent();
            }
            this.AddComponent(this.unitTransform);
            this.AddComponent(this.moveTrans);
            this.AddComponent(beHit);

            beHit.RegisterBeHitCallBack(this.BeHitCallBack);
        }

        public override void InitComponents()
        {
            base.InitComponents();
            this.unitTransform.SetBodyRadius(this.birthParam["bodyRadius"]);
        }

        public void BeHitCallBack(BattleUnit battleUnit)
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
                this.eventDipatcher.DispatchEvent(BattleEvent.MONSTER_LIVE_REDUCE);
                if (this.curLive <= 0)
                {
                    this.isHaveDead = true;
                    this.eventDipatcher.DispatchEvent<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this);
                    return;
                }
            }
        }

        public bool IsDead()
        {
            if (this.moveTrans.isReachCarrot == true)
            {
                return true;
            }
            if (this.curLive <= 0)
            {
                return true;
            }
            return false;
        }

        public override void OnTick(Fix64 deltaTime)
        {
            this.moveTrans.OnTick(deltaTime);
            this.EndPointDistance = this.moveTrans.EndPointDistance;
        }

        public override void LateTick(Fix64 deltaTime)
        {
            this.unitTransform.LateTick(deltaTime);
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            this.curLevel = 0;
            this.monsterId = 0;
            this.isHaveDead = false;
            this.haveBeHit.Clear();
        }
    }
}
