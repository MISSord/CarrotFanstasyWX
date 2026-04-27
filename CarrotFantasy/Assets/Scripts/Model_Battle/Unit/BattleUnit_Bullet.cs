using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleUnit_Bullet : BattleUnit
    {
        public int damage { get; private set; }
        public Fix64 moveSpeed;

        public int towerId = 0;
        public int towerLevel = 0;

        public UnitMoveComponent_Bullet moveComponent;
        public UnitTransformComponent tranComponent;
        public UnitBeHitComponent beHitComponent;

        private BattleUnit target;

        public BattleUnit_Bullet(BaseBattle battle) : base(battle)
        {
            this.unitType = BattleUnitType.BULLET;
        }

        public override void LoadInfo(int uid, Dictionary<string, Fix64> param, Fix64Vector2 birthPosition)
        {
            base.LoadInfo(uid, param, birthPosition);
            this.damage = (int)this.birthParam["damage"];
            this.moveSpeed = this.birthParam["moveSpeed"];
        }

        public void LoadInfo2(BattleUnit_Tower tower, BattleUnit target)
        {
            this.towerId = tower.towerID;
            this.towerLevel = tower.curLevel;
            this.target = target;
        }

        public override void Init()
        {
            if (this.towerId == 4)
            {
                this.moveComponent = BattleUnitPool.Instance.getNewUnitComponent<UnitMoveComponent_Bullet>(UnitComponentType.MOVE_BULLET);
                if (this.moveComponent == null)
                {
                    this.moveComponent = new UnitMoveComponent_Bullet();
                }
            }
            else
            {
                this.moveComponent = BattleUnitPool.Instance.getNewUnitComponent<UnitMoveComponent_Bullet_One>(UnitComponentType.MOVE_BULLET_ONE);
                if (this.moveComponent == null)
                {
                    this.moveComponent = new UnitMoveComponent_Bullet_One();
                }
            }

            this.tranComponent = BattleUnitPool.Instance.getNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.tranComponent == null)
            {
                this.tranComponent = new UnitTransformComponent();
            }
            this.beHitComponent = BattleUnitPool.Instance.getNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
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
            this.tranComponent.SetBodyRadius(new Fix64(0.2f));
        }

        private void BeHitCallBack(BattleUnit unit)
        {
            if (unit.unitType.Equals(BattleUnitType.MONSTER) == true || unit.unitType.Equals(BattleUnitType.ITEM) == true)
            {
                if (this.birthParam["isRemove"] == Fix64.Zero)
                {
                    this.eventDipatcher.DispatchEvent<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this);
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

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
