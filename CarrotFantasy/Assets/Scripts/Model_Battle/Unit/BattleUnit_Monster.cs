using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleUnit_Monster : BattleUnit
    {
        public int curLive;
        public int totalLive;
        protected UnitTransformComponent unitTransform;

        /// <summary>具体为 <see cref="UnitMoveComponent_Monster"/> 或子类替换的移动组件。</summary>
        protected BaseUnitComponent locomotionComponent;

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

        private IMonsterLocomotion Locomotion
        {
            get { return (IMonsterLocomotion)this.locomotionComponent; }
        }

        /// <summary>回收到对象池时使用的池键（经典怪 / 流场怪）。</summary>
        public static string GetMonsterPoolKey(BattleUnit_Monster monster)
        {
            return monster is BattleUnit_MonsterFlow ? BattleUnitType.MONSTER_FLOW : BattleUnitType.MONSTER;
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

        public virtual void LoadInfo3(List<Fix64Vector2> monsterPath, Fix64 distance)
        {
            ((UnitMoveComponent_Monster)this.locomotionComponent).LoadInfo(monsterPath, distance);
        }

        public override void Init()
        {
            this.unitTransform = BattleUnitPool.Instance.GetNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.unitTransform == null)
            {
                this.unitTransform = new UnitTransformComponent();
            }

            this.InstallLocomotion();

            UnitBeHitComponent beHit = BattleUnitPool.Instance.GetNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (beHit == null)
            {
                beHit = new UnitBeHitComponent();
            }

            this.AddComponent(this.unitTransform);
            this.AddComponent(this.locomotionComponent);
            this.AddComponent(beHit);

            beHit.RegisterBeHitCallBack(this.BeHitCallBack);
        }

        protected virtual void InstallLocomotion()
        {
            UnitMoveComponent_Monster m = BattleUnitPool.Instance.GetNewUnitComponent<UnitMoveComponent_Monster>(UnitComponentType.MOVE_MONSTER);
            if (m == null)
            {
                m = new UnitMoveComponent_Monster();
            }

            this.locomotionComponent = m;
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
                this.eventDipatcher.DispatchEvent<int>(BattleEvent.MONSTER_DAMAGE_NUMBER, bullet.damage);
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
            if (this.Locomotion.isReachCarrot == true)
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
            this.Locomotion.OnTick(deltaTime);
            this.EndPointDistance = this.Locomotion.EndPointDistance;
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
            if (this.locomotionComponent != null)
            {
                this.Locomotion.ClearMovementState();
            }
        }
    }
}
