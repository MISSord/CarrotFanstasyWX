using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 子弹列表 Tick：本帧在 Tower 阶段之后移动；同帧新建的弹也会在本段 OnTick 移动一次。
    /// 销毁在 Tick 末尾统一回收。流程见 BattleCombatFlow.md。
    /// </summary>
    public class BattleBulletComponent : BaseBattleComponent
    {
        private List<BattleUnit_Bullet> curBulletList = new List<BattleUnit_Bullet>();
        private List<BattleUnit_Bullet> bulletDeadList = new List<BattleUnit_Bullet>();

        private BulletConfigReader configReader;

        public BattleBulletComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.BulletComponent;
            this.configReader = BulletConfigReader.Instance;
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
            if (tower == null || target == null)
            {
                return;
            }

            BattleUnit_Bullet bullet = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Bullet>(BattleUnitType.BULLET);
            if (bullet == null)
            {
                bullet = new BattleUnit_Bullet(this.baseBattle);
            }

            Dictionary<string, Fix64> bulletParam =
                this.configReader.GetSingleBulletConfig(tower.towerID * 100 + tower.curLevel + 1);
            if (bulletParam == null)
            {
                return;
            }

            ApplyGlobalTowerDamageBonus(bulletParam);
            bullet.LoadInfo(this.baseBattle.GetUid(), bulletParam, tower.birthPosition);
            bullet.LoadInfo2(tower, target);
            bullet.eventDipatcher.AddListener<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this.AddDeadList);
            bullet.Init();
            bullet.InitComponents();
            this.curBulletList.Add(bullet);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.BULLET, bullet);
        }

        private void ApplyGlobalTowerDamageBonus(Dictionary<string, Fix64> bulletParam)
        {
            if (bulletParam == null || !bulletParam.ContainsKey("damage"))
            {
                return;
            }

            BattleGlobalBuffComponent globalBuff = BattleGlobalBuffComponent.GetFrom(this.baseBattle);
            if (globalBuff == null)
            {
                return;
            }

            Fix64 multiplier = globalBuff.GetTowerDamageMultiplier();
            if (multiplier <= Fix64.One)
            {
                return;
            }

            int baseDamage = (int)bulletParam["damage"];
            int scaledDamage = (int)(new Fix64(baseDamage) * multiplier);
            if (scaledDamage < 1)
            {
                scaledDamage = 1;
            }

            bulletParam["damage"] = new Fix64(scaledDamage);
        }

        private void AddDeadList(BattleUnit_Bullet monster)
        {
            if (monster == null || this.bulletDeadList.Contains(monster))
            {
                return;
            }

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
