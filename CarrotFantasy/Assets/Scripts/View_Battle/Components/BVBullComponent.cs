using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVBulletComponent : BaseBattleViewComponent
    {
        private String prefabUrl;
        private GameObject rootGameObject;

        private Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet> bulletDic = new Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet>();
        public BVBulletComponent(BattleView_base battleView) : base(battleView)
        {
            this.prefabUrl = "Prefabs/Game/Tower/ID{0}/Bullect/{1}";
            this.componentType = BattleViewComponentType.BULLET;
        }

        public override void init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.getComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.registerGameContainer("BulletContainer");

            BattleDataComponent dataComponent = (BattleDataComponent)this.battle.getComponent(BattleComponentType.DataComponent);
            for (int i = 0; i < dataComponent.towerIDListLength; i++)
            {
                GameViewObjectPool.Instance.registerGameObject(String.Format("{0}_1", dataComponent.curTowerIDList[i]));
                GameViewObjectPool.Instance.registerGameObject(String.Format("{0}_2", dataComponent.curTowerIDList[i]));
                GameViewObjectPool.Instance.registerGameObject(String.Format("{0}_3", dataComponent.curTowerIDList[i]));
            }
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.registerNewBulletView);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.removeBulletView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.registerNewBulletView);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.removeBulletView);
        }

        private void registerNewBulletView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.BULLET))
            {
                BattleUnit_Bullet bullet = (BattleUnit_Bullet)unit;
                BattleUnitView_Bullet bulletView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Bullet>(BattleUnitViewType.Bullet);
                if (bulletView == null)
                {
                    bulletView = new BattleUnitView_Bullet();
                }
                GameObject bulletNode = GameViewObjectPool.Instance.getNewGameObject(String.Format("{0}_{1}", bullet.towerId, bullet.towerLevel + 1));
                if (bulletNode == null)
                {
                    bulletNode = GameObject.Instantiate(ResourceLoader.Instance.getGameObject(String.Format(this.prefabUrl, bullet.towerId, bullet.towerLevel + 1)));
                }
                bulletNode.transform.SetParent(this.rootGameObject.transform);
                bulletView.initTransform(bulletNode.transform);
                bulletView.loadInfo(this.battleView, bullet);
                bulletView.init();

                this.bulletDic.Add(bullet, bulletView);
            }
        }

        public override void onTick(float time)
        {
            foreach (KeyValuePair<BattleUnit_Bullet, BattleUnitView_Bullet> info in this.bulletDic)
            {
                info.Value.onTick(time);
            }
        }

        private void removeBulletView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.BULLET) == false) return;
            BattleUnit_Bullet bullet = (BattleUnit_Bullet)unit;
            BattleUnitView_Bullet bulletView;
            if (!this.bulletDic.TryGetValue(bullet, out bulletView))
            {
                Debug.Log("移除子弹视图出错");
                return;
            }
            GameViewObjectPool.Instance.pushGameObjectToPool(String.Format("{0}_{1}", bullet.towerId, bullet.towerLevel + 1), bulletView.transform.gameObject);
            bulletView.clearUnitInfo();
            this.bulletDic.Remove(bullet);
            GameViewObjectPool.Instance.pushViewObjectToPool(BattleUnitViewType.Bullet, bulletView);
        }

        public override void clearGameInfo()
        {
            foreach (KeyValuePair<BattleUnit_Bullet, BattleUnitView_Bullet> info in this.bulletDic)
            {
                GameViewObjectPool.Instance.pushGameObjectToPool(String.Format("{0}_{1}", info.Key.towerId, info.Key.towerLevel + 1), info.Value.transform.gameObject);
                info.Value.clearUnitInfo();
                GameViewObjectPool.Instance.pushViewObjectToPool(BattleUnitViewType.Bullet, info.Value);
            }
            this.bulletDic.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.clearGameInfo();
            base.Dispose();
        }
    }
}
