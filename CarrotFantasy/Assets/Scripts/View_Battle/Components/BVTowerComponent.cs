using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVTowerComponent : BaseBattleViewComponent
    {
        private String prefabUrl;

        private GameObject rootGameObject;

        public Dictionary<BattleUnit_Tower, BattleUnitView_Tower> towerViewDic = new Dictionary<BattleUnit_Tower, BattleUnitView_Tower>();

        private GameObject buildGameObject;
        private GameObject sellGameObject;

        private BattleSchedulerComponent scheComponent;

        public BVTowerComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.TOWER;
        }

        public override void Init()
        {
            this.prefabUrl = "Prefabs/Game/Tower/ID{0}/TowerSet/{1}";
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("TowerContainer");

            this.buildGameObject = ResourceLoader.Instance.GetGameObject("Prefabs/Game/BuildEffect");
            this.sellGameObject = ResourceLoader.Instance.GetGameObject("Prefabs/Game/DestoryEffect");

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
            BattleUnitView_Tower towerView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Tower>(BattleUnitViewType.Tower);
            if (towerView == null)
            {
                towerView = new BattleUnitView_Tower();
            }
            GameObject towerObj = GameObject.Instantiate(
                ResourceLoader.Instance.GetGameObject(String.Format(this.prefabUrl, tower.towerID, tower.curLevel + 1)));
            towerObj.transform.SetParent(this.rootGameObject.transform);
            towerView.LoadInfo(this.battleView, tower);
            towerView.InitTransform(towerObj.transform);
            tower.eventDipatcher.AddListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.ReloadTran);
            towerView.Init();
            this.towerViewDic.Add(tower, towerView);
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Tower/TowerBulid");
            GameObject build = GameObject.Instantiate(this.buildGameObject);
            UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            build.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

            scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(build); }, 0.5f);
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
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Tower/TowerSell");

            GameObject sell = GameObject.Instantiate(this.sellGameObject);
            UnitTransformComponent tran = (UnitTransformComponent)tower.GetComponent(UnitComponentType.TRANSFORM);
            sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

            scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(sell); }, 0.5f);
        }

        private void ReloadTran(BattleUnit_Tower tower)
        {
            BattleUnitView_Tower towerView = this.towerViewDic[tower];
            GameObject.Destroy(towerView.transform.gameObject);
            GameObject towerObj = GameObject.Instantiate(
                ResourceLoader.Instance.GetGameObject(String.Format(this.prefabUrl, tower.towerID, tower.curLevel + 1)));
            towerView.InitTransform(towerObj.transform);
            towerView.ReloadInfo();
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Tower/TowerUpdata");

            GameObject build = GameObject.Instantiate(this.buildGameObject);
            UnitTransformComponent tran = (UnitTransformComponent)tower.GetComponent(UnitComponentType.TRANSFORM);
            build.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);

            scheComponent.DelayExeOnceTimes(() => { GameObject.Destroy(build); }, 0.5f);
        }

        public override void ClearGameInfo()
        {
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
