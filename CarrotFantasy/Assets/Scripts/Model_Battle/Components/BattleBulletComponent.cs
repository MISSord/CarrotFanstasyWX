using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BattleBulletComponent : BaseBattleComponent
    {
        private List<BattleUnit_Bullet> curBulletList = new List<BattleUnit_Bullet>();
        private List<BattleUnit_Bullet> bulletDeadList = new List<BattleUnit_Bullet>();

        private BulletConfigReader configReader;

        public BattleBulletComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.BulletComponent;
            this.configReader = new BulletConfigReader();
            this.configReader.init();
        }

        public override void init()
        {
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<BattleUnit_Tower, BattleUnit>(BattleEvent.BULLET_BUILD, this.buildNewBullet);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.updateBullInfo);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<BattleUnit_Tower, BattleUnit>(BattleEvent.BULLET_BUILD, this.buildNewBullet);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.updateBullInfo);
        }

        public void buildNewBullet(BattleUnit_Tower tower, BattleUnit target)
        {
            BattleUnit_Bullet bullet = GameObjectPool.Instance.getNewBattleUnit<BattleUnit_Bullet>(BattleUnitType.BULLET);
            if (bullet == null)
            {
                bullet = new BattleUnit_Bullet(this.baseBattle);
            }
            bullet.eventDipatcher.AddListener<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this.addDeadList);
            bullet.loadInfo(this.baseBattle.getUid(), this.configReader.getSingleBulletConfig(tower.towerID * 100 + tower.curLevel + 1), tower.birthPosition);
            bullet.loadInfo2(tower, target);
            bullet.init();
            bullet.initComponents();
            this.curBulletList.Add(bullet);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.BULLET, bullet);
            //Debug.Log("注册新的子弹");
        }

        private void addDeadList(BattleUnit_Bullet monster)
        {
            this.bulletDeadList.Add(monster);
        }

        public override void onTick(Fix64 time)
        {
            base.onTick(time);
            this.updateCurBulletState(time);
        }

        public override void lateTick(Fix64 time)
        {
            base.lateTick(time);
            this.updateCurMonsterWaveStateLateTick(time);
        }

        private void updateBullInfo(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER)) return;
            for (int i = 0; i <= this.curBulletList.Count - 1; i++)
            {
                this.curBulletList[i].moveComponent.removeMoveDirect(unit);
            }
        }

        public void updateCurBulletState(Fix64 time)
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].onTick(time);
                }
            }
            if (this.bulletDeadList.Count != 0)
            {
                for (int i = 0; i < this.bulletDeadList.Count; i++)
                {
                    this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.BULLET, this.bulletDeadList[i]);
                    this.bulletDeadList[i].ClearInfo();
                    GameObjectPool.Instance.pushObjectToPool(BattleUnitType.BULLET, this.bulletDeadList[i]);
                    this.curBulletList.Remove(this.bulletDeadList[i]);
                }
                this.bulletDeadList.Clear();
            }
        }

        public void updateCurMonsterWaveStateLateTick(Fix64 time)
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].lateTick(time);
                }
            }
        }

        public override void clearInfo()
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].ClearInfo();
                    GameObjectPool.Instance.pushObjectToPool(BattleUnitType.BULLET, this.curBulletList[i]);
                }
            }
            this.curBulletList.Clear();
            this.bulletDeadList.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.clearInfo();
            base.Dispose();
        }
    }
}
