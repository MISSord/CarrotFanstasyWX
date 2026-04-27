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
            this.configReader.Init();
        }

        public override void Init()
        {
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<BattleUnit_Tower, BattleUnit>(BattleEvent.BULLET_BUILD, this.BuildNewBullet);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.UpdateBullInfo);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<BattleUnit_Tower, BattleUnit>(BattleEvent.BULLET_BUILD, this.BuildNewBullet);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.UpdateBullInfo);
        }

        public void BuildNewBullet(BattleUnit_Tower tower, BattleUnit target)
        {
            BattleUnit_Bullet bullet = BattleUnitPool.Instance.getNewBattleUnit<BattleUnit_Bullet>(BattleUnitType.BULLET);
            if (bullet == null)
            {
                bullet = new BattleUnit_Bullet(this.baseBattle);
            }
            bullet.eventDipatcher.AddListener<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this.AddDeadList);
            bullet.LoadInfo(this.baseBattle.GetUid(), this.configReader.getSingleBulletConfig(tower.towerID * 100 + tower.curLevel + 1), tower.birthPosition);
            bullet.LoadInfo2(tower, target);
            bullet.Init();
            bullet.InitComponents();
            this.curBulletList.Add(bullet);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.BULLET, bullet);
            //Debug.Log("注册新的子弹");
        }

        private void AddDeadList(BattleUnit_Bullet monster)
        {
            this.bulletDeadList.Add(monster);
        }

        public override void OnTick(Fix64 time)
        {
            base.OnTick(time);
            this.UpdateCurBulletState(time);
        }

        public override void LateTick(Fix64 time)
        {
            base.LateTick(time);
            this.UpdateCurMonsterWaveStateLateTick(time);
        }

        private void UpdateBullInfo(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER)) return;
            for (int i = 0; i <= this.curBulletList.Count - 1; i++)
            {
                this.curBulletList[i].moveComponent.RemoveMoveDirect(unit);
            }
        }

        public void UpdateCurBulletState(Fix64 time)
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].OnTick(time);
                }
            }
            if (this.bulletDeadList.Count != 0)
            {
                for (int i = 0; i < this.bulletDeadList.Count; i++)
                {
                    this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.BULLET, this.bulletDeadList[i]);
                    this.bulletDeadList[i].ClearInfo();
                    BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.BULLET, this.bulletDeadList[i]);
                    this.curBulletList.Remove(this.bulletDeadList[i]);
                }
                this.bulletDeadList.Clear();
            }
        }

        public void UpdateCurMonsterWaveStateLateTick(Fix64 time)
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].LateTick(time);
                }
            }
        }

        public override void ClearInfo()
        {
            if (this.curBulletList.Count != 0)
            {
                for (int i = 0; i < this.curBulletList.Count; i++)
                {
                    this.curBulletList[i].ClearInfo();
                    BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.BULLET, this.curBulletList[i]);
                }
            }
            this.curBulletList.Clear();
            this.bulletDeadList.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
