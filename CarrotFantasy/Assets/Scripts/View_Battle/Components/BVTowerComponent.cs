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
        private GameObject sellGameObject;
        private AssetLoadHandle _buildEffectHandle;
        private AssetLoadHandle _sellEffectHandle;

        private readonly Dictionary<string, GameObject> _towerPrefabTemplates = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, AssetLoadHandle> _towerPrefabHandles = new Dictionary<string, AssetLoadHandle>();

        private BattleSchedulerComponent scheComponent;

        public BVTowerComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.TOWER;
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("TowerContainer");

            this.buildGameObject = GameObjectResourceManager.Instance.LoadPrefabBlocking(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.BuildEffect, out _buildEffectHandle);
            this.sellGameObject = GameObjectResourceManager.Instance.LoadPrefabBlocking(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect, out _sellEffectHandle);

            this.scheComponent = (BattleSchedulerComponent)this.battle.GetComponent(BattleComponentType.SchedulerComponent);

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

        private GameObject GetTowerPrefabTemplate(int towerId, int towerLevelIndex)
        {
            string bundleName = FightViewPrefabAb.TowerSetBundleName(towerId);
            string assetName = towerLevelIndex.ToString();
            string cacheKey = bundleName + "/" + assetName;
            if (_towerPrefabTemplates.TryGetValue(cacheKey, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject tpl = GameObjectResourceManager.Instance.LoadPrefabBlocking(bundleName, assetName, out AssetLoadHandle handle);
            if (tpl == null)
            {
                Debug.LogError($"[BVTowerComponent] 防御塔预制体加载失败: bundle={bundleName}, asset={assetName}");
                return null;
            }

            _towerPrefabTemplates[cacheKey] = tpl;
            _towerPrefabHandles[cacheKey] = handle;
            return tpl;
        }

        private void RegisterTowerView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER) == false) return;
            BattleUnit_Tower tower = (BattleUnit_Tower)unit;
            BattleUnitView_Tower towerView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Tower>(BattleUnitViewType.Tower);
            if (towerView == null)
            {
                towerView = new BattleUnitView_Tower();
            }
            GameObject towerTpl = GetTowerPrefabTemplate(tower.towerID, tower.curLevel + 1);
            if (towerTpl == null)
            {
                return;
            }

            GameObject towerObj = GameObject.Instantiate(towerTpl);
            towerObj.transform.SetParent(this.rootGameObject.transform);
            towerView.LoadInfo(this.battleView, tower);
            towerView.InitTransform(towerObj.transform);
            tower.eventDipatcher.AddListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
            towerView.Init();
            this.towerViewDic.Add(tower, towerView);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerBulid");
            if (this.buildGameObject != null)
            {
                GameObject build = GameObject.Instantiate(this.buildGameObject);
                UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
                build.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

                scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(build); }, 0.5f);
            }
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

            GameObject.Destroy(towerView.transform.gameObject);
            towerView.ClearUnitInfo();
            tower.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);

            this.towerViewDic.Remove(tower);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Tower, towerView);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerSell");

            if (this.sellGameObject != null)
            {
                GameObject sell = GameObject.Instantiate(this.sellGameObject);
                UnitTransformComponent tran = (UnitTransformComponent)tower.GetComponent(UnitComponentType.TRANSFORM);
                sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

                scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(sell); }, 0.5f);
            }
        }

        private void ReloadTran(BattleUnit_Tower tower)
        {
            BattleUnitView_Tower towerView = this.towerViewDic[tower];
            GameObject.Destroy(towerView.transform.gameObject);
            GameObject towerTpl = GetTowerPrefabTemplate(tower.towerID, tower.curLevel + 1);
            if (towerTpl == null)
            {
                return;
            }

            GameObject towerObj = GameObject.Instantiate(towerTpl);
            towerView.InitTransform(towerObj.transform);
            towerView.ReloadInfo();
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerUpdata");

            if (this.buildGameObject != null)
            {
                GameObject build = GameObject.Instantiate(this.buildGameObject);
                UnitTransformComponent tran = (UnitTransformComponent)tower.GetComponent(UnitComponentType.TRANSFORM);
                build.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

                scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(build); }, 0.5f);
            }
        }

        public override void ClearGameInfo()
        {
            foreach (KeyValuePair<string, AssetLoadHandle> kv in _towerPrefabHandles)
            {
                kv.Value.Dispose();
            }

            _towerPrefabHandles.Clear();
            _towerPrefabTemplates.Clear();
            if (_buildEffectHandle.IsValid)
            {
                _buildEffectHandle.Dispose();
                _buildEffectHandle = AssetLoadHandle.Invalid;
            }

            if (_sellEffectHandle.IsValid)
            {
                _sellEffectHandle.Dispose();
                _sellEffectHandle = AssetLoadHandle.Invalid;
            }

            foreach (KeyValuePair<BattleUnit_Tower, BattleUnitView_Tower> info in this.towerViewDic)
            {
                GameObject.Destroy(info.Value.transform.gameObject);
                info.Value.ClearUnitInfo();
                info.Key.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
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
