using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleUnit_Bullet : BattleUnit
    {
        public int damage { get; private set; }
        public int onHitBuffId { get; private set; }
        public Fix64 moveSpeed;

        public int towerId = 0;
        public int towerLevel = 0;

        public UnitMoveComponent_Bullet moveComponent;
        public UnitTransformComponent tranComponent;
        public UnitBeHitComponent beHitComponent;

        private BattleUnit target;
        private bool isQueuedForRemove;

        public BattleUnit_Bullet(BaseBattle battle) : base(battle)
        {
            this.unitType = BattleUnitType.BULLET;
        }

        public override void LoadInfo(int uid, Dictionary<string, Fix64> param, Fix64Vector2 birthPosition)
        {
            base.LoadInfo(uid, param, birthPosition);
            this.damage = (int)this.birthParam["damage"];
            this.moveSpeed = this.birthParam["moveSpeed"];
            this.onHitBuffId = param.ContainsKey("onHitBuffId") ? (int)param["onHitBuffId"] : 0;
        }

        public bool DestroyOnFirstHit()
        {
            return this.birthParam != null && this.birthParam["isRemove"] == Fix64.Zero;
        }

        public void RequestRemove()
        {
            if (this.isQueuedForRemove || this.eventDipatcher == null)
            {
                return;
            }

            this.isQueuedForRemove = true;
            this.eventDipatcher.DispatchEvent<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this);
        }

        public void LoadInfo2(BattleUnit_Tower tower, BattleUnit target)
        {
            this.towerId = tower.towerID;
            this.towerLevel = tower.curLevel;
            this.target = target;
        }

        public override void Init()
        {
            this.moveComponent = BulletMoveComponentFactory.CreateFromBirthParam(this.birthParam);

            this.tranComponent = BattleUnitPool.Instance.GetNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.tranComponent == null)
            {
                this.tranComponent = new UnitTransformComponent();
            }
            this.beHitComponent = BattleUnitPool.Instance.GetNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (this.beHitComponent == null)
            {
                this.beHitComponent = new UnitBeHitComponent();
            }

            this.AddComponent(this.moveComponent);
            this.AddComponent(this.tranComponent);
            this.AddComponent(this.beHitComponent);

            this.beHitComponent.RegisterBeHitCallBack(this.BeHitCallBack);
        }

        public override void InitComponents()
        {
            base.InitComponents();
            this.moveComponent.RegisterMoveDirect(this.target);
            // 逻辑碰撞半径来自 tbbullet.BodyRadius，与 HitTest / TryResolveTargetHit 一致。
            Fix64 bodyRadius = this.birthParam != null && this.birthParam.ContainsKey("bodyRadius")
                ? this.birthParam["bodyRadius"]
                : new Fix64(0.2f);
            this.tranComponent.SetBodyRadius(bodyRadius);
        }

        private void BeHitCallBack(BattleUnit unit)
        {
            if (unit.unitType.Equals(BattleUnitType.MONSTER))
            {
                BattleUnit_Monster monster = (BattleUnit_Monster)unit;
                if (monster.IsDamageImmune())
                {
                    return;
                }

                if (this.DestroyOnFirstHit())
                {
                    this.RequestRemove();
                }

                return;
            }

            if (unit.unitType.Equals(BattleUnitType.ITEM))
            {
                BattleUnit_Item item = (BattleUnit_Item)unit;
                if (item.IsDead())
                {
                    return;
                }

                if (this.DestroyOnFirstHit())
                {
                    this.RequestRemove();
                }
            }
        }

        public override void OnTick(Fix64 deltaTime)
        {
            this.moveComponent.OnTick(deltaTime);
        }

        public override void LateTick(Fix64 deltaTime)
        {
            this.tranComponent.LateTick(deltaTime);
        }

        public override void ClearInfo()
        {
            this.isQueuedForRemove = false;
            this.onHitBuffId = 0;
            this.towerId = 0;
            this.towerLevel = 0;
            this.target = null;
            base.ClearInfo();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
