using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVTowerComponent : BaseBattleViewComponent
    {
        private GameObject rootGameObject;

        public Dictionary<BattleUnit_Tower, BattleUnitView_Tower> towerViewDic = new Dictionary<BattleUnit_Tower, BattleUnitView_Tower>();

        private GameObject buildGameObject;

        private readonly HashSet<string> _registeredTowerPoolKeys = new HashSet<string>();

        private BattleSchedulerComponent scheComponent;

        public BVTowerComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.TOWER;
        }

        public override void Init()
        {
            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVTowerComponent] BVSceneComponent 未注册。");
                return;
            }

            this.rootGameObject = scene.RegisterGameContainer("TowerContainer");

            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.BuildEffect,
                out this.buildGameObject))
            {
                Debug.LogError("[BVTowerComponent] BuildEffect 未预加载");
            }

            this.scheComponent = (BattleSchedulerComponent)this.battle.GetComponent(BattleComponentType.SchedulerComponent);
            BattleViewEffectHelper.EnsureDestroyEffectPoolRegistered();
            GameViewObjectPool.Instance.PurgeLegacyNumericPoolKeys();
            this.RemoveListener();
            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterTowerView);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveTowerView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterTowerView);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveTowerView);
        }

        void EnsureTowerPoolRegistered(string poolKey)
        {
            if (this._registeredTowerPoolKeys.Add(poolKey))
            {
                GameViewObjectPool.Instance.RegisterGameObject(poolKey);
            }
        }

        private GameObject GetTowerPrefabTemplate(int towerId, int towerLevelIndex)
        {
            string bundleName = FightViewPrefabAb.FightPartTowerBundle;
            string assetName = FightViewPrefabAb.TowerAssetName(towerId, towerLevelIndex);
            GameObject tpl;
            if (BattleViewPrefabPreloader.TryGetTemplate(bundleName, assetName, out tpl))
            {
                return tpl;
            }

            Debug.LogError($"[BVTowerComponent] 防御塔预制体未预加载: bundle={bundleName}, asset={assetName}");
            return null;
        }

        private GameObject TryPopTowerVisual(string poolKey)
        {
            while (true)
            {
                GameObject candidate = GameViewObjectPool.Instance.GetNewGameObject(poolKey);
                if (candidate == null)
                {
                    return null;
                }

                if (FightViewGameObjectPoolKeys.IsTowerVisual(candidate))
                {
                    return candidate;
                }

                Debug.LogWarning(String.Format(
                    "[BVTowerComponent] 对象池 {0} 弹出非塔预制体，已销毁: {1}",
                    poolKey,
                    candidate.name));
                GameObject.Destroy(candidate);
            }
        }

        private GameObject RentTowerGameObject(int towerId, int towerLevelIndex)
        {
            string poolKey = FightViewGameObjectPoolKeys.Tower(towerId, towerLevelIndex);
            this.EnsureTowerPoolRegistered(poolKey);

            GameObject towerObj = TryPopTowerVisual(poolKey);
            if (towerObj == null)
            {
                GameObject towerTpl = GetTowerPrefabTemplate(towerId, towerLevelIndex);
                if (towerTpl == null)
                {
                    return null;
                }

                towerObj = GameObject.Instantiate(towerTpl);
            }

            towerObj.transform.SetParent(this.rootGameObject.transform);
            return towerObj;
        }

        private void ReturnTowerGameObject(int towerId, int towerLevelIndex, GameObject towerObj)
        {
            if (towerObj == null)
            {
                return;
            }

            if (!FightViewGameObjectPoolKeys.IsTowerVisual(towerObj))
            {
                GameObject.Destroy(towerObj);
                return;
            }

            string poolKey = FightViewGameObjectPoolKeys.Tower(towerId, towerLevelIndex);
            GameViewObjectPool.Instance.PushGameObjectToPool(poolKey, towerObj);
        }

        private void RegisterTowerView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER) == false) return;
            BattleUnit_Tower tower = (BattleUnit_Tower)unit;
            if (this.towerViewDic.ContainsKey(tower))
            {
                return;
            }

            BattleUnitView_Tower towerView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Tower>(BattleUnitViewType.Tower);
            if (towerView == null)
            {
                towerView = new BattleUnitView_Tower();
            }

            int levelIndex = tower.curLevel + 1;
            GameObject towerObj = RentTowerGameObject(tower.towerID, levelIndex);
            if (towerObj == null || !FightViewGameObjectPoolKeys.IsTowerVisual(towerObj))
            {
                if (towerObj != null)
                {
                    GameObject.Destroy(towerObj);
                }

                Debug.LogError(String.Format(
                    "[BVTowerComponent] 防御塔视图创建失败: towerId={0}, level={1}",
                    tower.towerID,
                    levelIndex));
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Tower, towerView);
                return;
            }

            towerView.LoadInfo(this.battleView, tower);
            towerView.InitTransform(towerObj.transform);
            tower.eventDipatcher.AddListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
            towerView.Init();
            this.towerViewDic.Add(tower, towerView);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerBulid");
            this.PlayBuildEffect(unit);
        }

        private void RemoveTowerView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER) == false) return;
            BattleUnit_Tower tower = (BattleUnit_Tower)unit;
            BattleUnitView_Tower towerView;
            if (!this.towerViewDic.TryGetValue(tower, out towerView))
            {
                Debug.Log("移除防御塔视图出错");
                return;
            }

            int levelIndex = tower.curLevel + 1;
            GameObject towerObj = towerView.transform != null ? towerView.transform.gameObject : null;
            towerView.ClearUnitInfo();
            tower.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
            this.ReturnTowerGameObject(tower.towerID, levelIndex, towerObj);

            this.towerViewDic.Remove(tower);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Tower, towerView);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerSell");
            BattleViewEffectHelper.PlayDestroyAt(tower);
        }

        private void ReloadTran(BattleUnit_Tower tower)
        {
            BattleUnitView_Tower towerView = this.towerViewDic[tower];
            int oldLevelIndex = tower.curLevel;
            GameObject oldObj = towerView.transform != null ? towerView.transform.gameObject : null;
            this.ReturnTowerGameObject(tower.towerID, oldLevelIndex, oldObj);

            int newLevelIndex = tower.curLevel + 1;
            GameObject towerObj = RentTowerGameObject(tower.towerID, newLevelIndex);
            if (towerObj == null)
            {
                return;
            }

            towerView.InitTransform(towerObj.transform);
            towerView.ReloadInfo();
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerUpdata");
            this.PlayBuildEffect(tower);
        }

        private void PlayBuildEffect(BattleUnit unit)
        {
            if (this.buildGameObject == null || this.scheComponent == null)
            {
                return;
            }

            GameObject build = GameObject.Instantiate(this.buildGameObject);
            UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            build.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
            this.scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(build); }, 0.5f);
        }

        public override void ClearGameInfo()
        {
            buildGameObject = null;
            _registeredTowerPoolKeys.Clear();

            foreach (KeyValuePair<BattleUnit_Tower, BattleUnitView_Tower> info in this.towerViewDic)
            {
                BattleUnit_Tower tower = info.Key;
                int levelIndex = tower.curLevel + 1;
                GameObject towerObj = info.Value.transform != null ? info.Value.transform.gameObject : null;
                info.Value.ClearUnitInfo();
                info.Key.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
                this.ReturnTowerGameObject(tower.towerID, levelIndex, towerObj);
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Tower, info.Value);
            }
            this.towerViewDic.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
