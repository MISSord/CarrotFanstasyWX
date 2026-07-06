using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVBulletComponent : BaseBattleViewComponent
    {
        private GameObject rootGameObject;

        private Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet> bulletDic = new Dictionary<BattleUnit_Bullet, BattleUnitView_Bullet>();

        public BVBulletComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.BULLET;
        }

        public override void Init()
        {
            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVBulletComponent] BVSceneComponent 未注册。");
                return;
            }

            this.rootGameObject = scene.RegisterGameContainer("BulletContainer");

            this.EnsureBulletPoolRegistrations();
            this.RemoveListener();
            this.AddListener();
            this.IsBuilt = true;
        }

        void EnsureBulletPoolRegistrations()
        {
            BattleDataComponent dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            if (dataComponent == null)
            {
                return;
            }

            for (int i = 0; i < dataComponent.towerIDListLength; i++)
            {
                int towerId = dataComponent.curTowerIDList[i];
                GameViewObjectPool.Instance.RegisterGameObject(FightViewGameObjectPoolKeys.Bullet(towerId, 1));
                GameViewObjectPool.Instance.RegisterGameObject(FightViewGameObjectPoolKeys.Bullet(towerId, 2));
                GameViewObjectPool.Instance.RegisterGameObject(FightViewGameObjectPoolKeys.Bullet(towerId, 3));
            }
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBulletView);
            this.eventDispatcher.AddListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBulletView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBulletView);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBulletView);
        }

        private GameObject GetBulletPrefabTemplate(int towerId, int bulletLevelIndex)
        {
            string bundleName = FightViewPrefabAb.FightPartBulletBundle;
            string assetName = FightViewPrefabAb.BulletAssetName(towerId, bulletLevelIndex);
            GameObject tpl;
            if (BattleViewPrefabPreloader.TryGetTemplate(bundleName, assetName, out tpl))
            {
                return tpl;
            }

            Debug.LogError($"[BVBulletComponent] 子弹预制体未预加载: bundle={bundleName}, asset={assetName}");
            return null;
        }

        private GameObject TryPopBulletVisual(string poolKey)
        {
            while (true)
            {
                GameObject candidate = GameViewObjectPool.Instance.GetNewGameObject(poolKey);
                if (candidate == null)
                {
                    return null;
                }

                if (FightViewGameObjectPoolKeys.IsBulletVisual(candidate))
                {
                    return candidate;
                }

                Debug.LogWarning(String.Format(
                    "[BVBulletComponent] 对象池 {0} 弹出非子弹预制体，已销毁: {1}",
                    poolKey,
                    candidate.name));
                GameObject.Destroy(candidate);
            }
        }

        private void ReturnBulletGameObject(int towerId, int bulletLevelIndex, GameObject bulletObj)
        {
            if (bulletObj == null)
            {
                return;
            }

            if (!FightViewGameObjectPoolKeys.IsBulletVisual(bulletObj))
            {
                GameObject.Destroy(bulletObj);
                return;
            }

            string poolKey = FightViewGameObjectPoolKeys.Bullet(towerId, bulletLevelIndex);
            GameViewObjectPool.Instance.PushGameObjectToPool(poolKey, bulletObj);
        }

        private void RegisterNewBulletView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.BULLET))
            {
                BattleUnit_Bullet bullet = (BattleUnit_Bullet)unit;
                if (this.bulletDic.ContainsKey(bullet))
                {
                    return;
                }

                BattleUnitView_Bullet bulletView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Bullet>(BattleUnitViewType.Bullet);
                if (bulletView == null)
                {
                    bulletView = new BattleUnitView_Bullet();
                }

                int bulletLevelIndex = bullet.towerLevel + 1;
                string poolKey = FightViewGameObjectPoolKeys.Bullet(bullet.towerId, bulletLevelIndex);
                GameObject bulletNode = TryPopBulletVisual(poolKey);
                if (bulletNode == null)
                {
                    GameObject tpl = GetBulletPrefabTemplate(bullet.towerId, bulletLevelIndex);
                    bulletNode = tpl != null ? GameObject.Instantiate(tpl) : null;
                }

                if (bulletNode == null)
                {
                    GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Bullet, bulletView);
                    return;
                }

                BattleView_base.AttachPooledVisualToContainer(bulletNode.transform, this.rootGameObject.transform);
                bulletView.InitTransform(bulletNode.transform);
                bulletView.LoadInfo(this.battleView, bullet);
                bulletView.Init();
                bulletView.ReloadInfo();

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
                return;
            }

            if (bulletView.transform != null)
            {
                this.ReturnBulletGameObject(bullet.towerId, bullet.towerLevel + 1, bulletView.transform.gameObject);
            }
            bulletView.ClearUnitInfo();
            this.bulletDic.Remove(bullet);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Bullet, bulletView);
        }

        public override void ReturnUnitsToPoolForReplay()
        {
            this.RemoveListener();
            this.ReturnAllBulletsToPool();
            GameViewObjectPool.Instance.RegisterBattleUnitView(BattleUnitViewType.Bullet);
            this.EnsureBulletPoolRegistrations();
        }

        public override void ApplyModelForReplay()
        {
            this.RebindBattleListeners(this.RemoveListener, this.AddListener);
        }

        void ReturnAllBulletsToPool()
        {
            foreach (KeyValuePair<BattleUnit_Bullet, BattleUnitView_Bullet> info in this.bulletDic)
            {
                this.ReturnBulletGameObject(
                    info.Key.towerId,
                    info.Key.towerLevel + 1,
                    info.Value.transform != null ? info.Value.transform.gameObject : null);
                info.Value.ClearUnitInfo();
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Bullet, info.Value);
            }

            this.bulletDic.Clear();
        }

        public override void ClearGameInfo()
        {
            this.ReturnAllBulletsToPool();
            this.RemoveListener();
            this.IsBuilt = false;
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
