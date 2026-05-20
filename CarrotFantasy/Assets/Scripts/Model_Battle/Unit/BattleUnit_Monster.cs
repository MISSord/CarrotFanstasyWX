using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 怪物单位基类：生命、受击、位移组件挂载与各玩法共用的 Tick。
    /// 经典 PVE 折线路径见 <see cref="BattleUnit_Monster_Pve"/>；
    /// 流场 / 测试等玩法在各自子类中安装移动组件。
    /// </summary>
    public abstract class BattleUnit_Monster : BattleUnit
    {
        public int curLive;
        public int totalLive;
        protected UnitTransformComponent unitTransform;

        /// <summary>由子类 <see cref="InstallLocomotion"/> 安装，须实现 <see cref="IMonsterLocomotion"/>。</summary>
        protected BaseUnitComponent locomotionComponent;

        private List<int> haveBeHit;
        private bool isHaveDead = false;

        public int monsterId { get; protected set; }

        public int curLevel { get; protected set; }

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

        /// <summary>回收到对象池时使用的池键。</summary>
        public static string GetMonsterPoolKey(BattleUnit_Monster monster)
        {
            if (monster is BattleUnit_MonsterFlow)
            {
                return BattleUnitType.MONSTER_FLOW;
            }

            return BattleUnitType.MONSTER;
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

        /// <summary>子类安装本玩法对应的移动组件。</summary>
        protected abstract void InstallLocomotion();

        public override void InitComponents()
        {
            base.InitComponents();
            this.unitTransform.SetBodyRadius(this.birthParam["bodyRadius"]);
        }

        public void BeHitCallBack(BattleUnit battleUnit)
        {
            if (this.isHaveDead == true)
            {
                return;
            }

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
                }
            }
        }

        public virtual bool IsDead()
        {
            if (this.locomotionComponent != null && this.Locomotion.isReachCarrot)
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
