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
        private int _useEpoch;
        private readonly HashSet<BattleUnit_Tower> _waitingSpawn = new HashSet<BattleUnit_Tower>();

        private BattleSchedulerComponent scheComponent;

        public BVTowerComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.TOWER;
        }

        public override void CollectWarm(AssetBundleBatch batch)
        {
            batch.Keep<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.BuildEffect);
            batch.Keep<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect);
            BattleTowerComponent towers = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            if (towers == null || towers.canBuildTowerList == null)
            {
                return;
            }

            for (int i = 0; i < towers.canBuildTowerListLength; i++)
            {
                batch.Keep<GameObject>(FightViewPrefabAb.TowerSetBundleName(towers.canBuildTowerList[i]), "1");
            }
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("TowerContainer");
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

        private void RegisterTowerView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER) == false) return;
            BattleUnit_Tower tower = (BattleUnit_Tower)unit;
            int epoch = this._useEpoch;
            this._waitingSpawn.Add(tower);
            this.BundleScope.Use<GameObject>(
                FightViewPrefabAb.TowerSetBundleName(tower.towerID),
                (tower.curLevel + 1).ToString(),
                tpl =>
                {
                    if (epoch != this._useEpoch || tpl == null || !this._waitingSpawn.Remove(tower))
                    {
                        return;
                    }

                    this.SpawnTower(tower, tpl, true);
                });
        }

        private void SpawnTower(BattleUnit_Tower tower, GameObject towerTpl, bool playBuild)
        {
            BattleUnitView_Tower towerView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Tower>(BattleUnitViewType.Tower);
            if (towerView == null)
            {
                towerView = new BattleUnitView_Tower();
            }

            GameObject towerObj = GameObject.Instantiate(towerTpl);
            towerObj.transform.SetParent(this.rootGameObject.transform);
            towerView.LoadInfo(this.battleView, tower);
            towerView.InitTransform(towerObj.transform);
            tower.eventDipatcher.AddListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
            towerView.Init();
            this.towerViewDic.Add(tower, towerView);
            if (!playBuild)
            {
                return;
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerBulid");
            this.PlayEffect(this.GetEffect(ref this.buildGameObject, FightViewPrefabAb.BuildEffect), tower);
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
            this._waitingSpawn.Remove(tower);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Tower, towerView);
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerSell");

            this.PlayEffect(this.GetEffect(ref this.sellGameObject, FightViewPrefabAb.DestoryEffect), tower);
        }

        private void ReloadTran(BattleUnit_Tower tower)
        {
            if (!this.towerViewDic.TryGetValue(tower, out BattleUnitView_Tower towerView))
            {
                return;
            }

            GameObject.Destroy(towerView.transform.gameObject);
            int epoch = this._useEpoch;
            this.BundleScope.Use<GameObject>(
                FightViewPrefabAb.TowerSetBundleName(tower.towerID),
                (tower.curLevel + 1).ToString(),
                tpl =>
                {
                    if (epoch != this._useEpoch || tpl == null || !this.towerViewDic.ContainsKey(tower))
                    {
                        return;
                    }

                    GameObject towerObj = GameObject.Instantiate(tpl);
                    towerView.InitTransform(towerObj.transform);
                    towerView.ReloadInfo();
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/TowerUpdata");
                    this.PlayEffect(this.GetEffect(ref this.buildGameObject, FightViewPrefabAb.BuildEffect), tower);
                });
        }

        private GameObject GetEffect(ref GameObject cache, string assetName)
        {
            if (cache == null)
            {
                cache = this.BundleScope.Get<GameObject>(FightViewPrefabAb.FightPartBundle, assetName);
            }

            return cache;
        }

        private void PlayEffect(GameObject template, BattleUnit_Tower tower)
        {
            if (template == null || this.scheComponent == null)
            {
                return;
            }

            GameObject effect = GameObject.Instantiate(template);
            UnitTransformComponent tran = (UnitTransformComponent)tower.GetComponent(UnitComponentType.TRANSFORM);
            effect.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
            this.scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(effect); }, 0.5f);
        }

        public override void ClearGameInfo()
        {
            this._useEpoch++;
            this._waitingSpawn.Clear();
            this.buildGameObject = null;
            this.sellGameObject = null;

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
