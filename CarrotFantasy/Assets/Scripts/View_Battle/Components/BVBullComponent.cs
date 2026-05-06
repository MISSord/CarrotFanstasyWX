using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVBulletComponent : BaseBattleViewComponent
    {
        private GameObject rootGameObject;

        private Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet> bulletDic = new Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet>();

        private readonly Dictionary<string, GameObject> _bulletPrefabTemplates = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, AssetLoadHandle> _bulletPrefabHandles = new Dictionary<string, AssetLoadHandle>();
        public BVBulletComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.BULLET;
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("BulletContainer");

            BattleDataComponent dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            for (int i = 0; i < dataComponent.towerIDListLength; i++)
            {
                GameViewObjectPool.Instance.RegisterGameObject(String.Format("{0}_1", dataComponent.curTowerIDList[i]));
                GameViewObjectPool.Instance.RegisterGameObject(String.Format("{0}_2", dataComponent.curTowerIDList[i]));
                GameViewObjectPool.Instance.RegisterGameObject(String.Format("{0}_3", dataComponent.curTowerIDList[i]));
            }
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBulletView);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBulletView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBulletView);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBulletView);
        }

        private GameObject GetBulletPrefabTemplate(int towerId, int bulletLevelIndex)
        {
            string bundleName = FightViewPrefabAb.TowerBulletBundleName(towerId);
            string assetName = bulletLevelIndex.ToString();
            string cacheKey = bundleName + "/" + assetName;
            if (_bulletPrefabTemplates.TryGetValue(cacheKey, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject tpl = GameObjectResourceManager.Instance.LoadPrefabBlocking(bundleName, assetName, out AssetLoadHandle handle);
            if (tpl == null)
            {
                Debug.LogError($"[BVBulletComponent] 子弹预制体加载失败: bundle={bundleName}, asset={assetName}");
                return null;
            }

            _bulletPrefabTemplates[cacheKey] = tpl;
            _bulletPrefabHandles[cacheKey] = handle;
            return tpl;
        }

        private void RegisterNewBulletView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.BULLET))
            {
                BattleUnit_Bullet bullet = (BattleUnit_Bullet)unit;
                BattleUnitView_Bullet bulletView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Bullet>(BattleUnitViewType.Bullet);
                if (bulletView == null)
                {
                    bulletView = new BattleUnitView_Bullet();
                }
                GameObject bulletNode = GameViewObjectPool.Instance.GetNewGameObject(String.Format("{0}_{1}", bullet.towerId, bullet.towerLevel + 1));
                if (bulletNode == null)
                {
                    GameObject tpl = GetBulletPrefabTemplate(bullet.towerId, bullet.towerLevel + 1);
                    bulletNode = tpl != null ? GameObject.Instantiate(tpl) : null;
                }

                if (bulletNode == null)
                {
                    return;
                }

                bulletNode.transform.SetParent(this.rootGameObject.transform);
                bulletView.InitTransform(bulletNode.transform);
                bulletView.LoadInfo(this.battleView, bullet);
                bulletView.Init();

                this.bulletDic.Add(bullet, bulletView);
            }
        }

        public override void OnTick(float time)
        {
            foreach (KeyValuePair<BattleUnit_Bullet, BattleUnitView_Bullet> info in this.bulletDic)
            {
                info.Value.OnTick(time);
            }
        }

        private void RemoveBulletView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.BULLET) == false) return;
            BattleUnit_Bullet bullet = (BattleUnit_Bullet)unit;
            BattleUnitView_Bullet bulletView;
            if (!this.bulletDic.TryGetValue(bullet, out bulletView))
            {
                Debug.Log("移除子弹视图出错");
                return;
            }
            GameViewObjectPool.Instance.PushGameObjectToPool(String.Format("{0}_{1}", bullet.towerId, bullet.towerLevel + 1), bulletView.transform.gameObject);
            bulletView.ClearUnitInfo();
            this.bulletDic.Remove(bullet);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Bullet, bulletView);
        }

        public override void ClearGameInfo()
        {
            foreach (KeyValuePair<string, AssetLoadHandle> kv in _bulletPrefabHandles)
            {
                kv.Value.Dispose();
            }

            _bulletPrefabHandles.Clear();
            _bulletPrefabTemplates.Clear();
            foreach (KeyValuePair<BattleUnit_Bullet, BattleUnitView_Bullet> info in this.bulletDic)
            {
                GameViewObjectPool.Instance.PushGameObjectToPool(String.Format("{0}_{1}", info.Key.towerId, info.Key.towerLevel + 1), info.Value.transform.gameObject);
                info.Value.ClearUnitInfo();
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Bullet, info.Value);
            }
            this.bulletDic.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
