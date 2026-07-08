using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVMapComponent : BaseBattleViewComponent
    {
        public Sprite sprGirdNoramlState;
        public Sprite sprGirdStartState;
        public Sprite sprGirdCantBuildState;
        public GridPoint[,] gridPointList;

        //地图的有关属性
        //地图
        public int xColumn;
        public int yRow;

        private BattleTowerComponent towerComponent;
        private BattleDataComponent dataComponent;
        private int pendingRemoveGridX = -1;
        private int pendingRemoveGridY = -1;
        private readonly HashSet<BattleUnit_Tower> levelUpSignalTowers = new HashSet<BattleUnit_Tower>();

        public BVMapComponent(BattleView_base battleView) : base(battleView)
        {
            BattleDataComponent dataOne = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.xColumn = dataOne.xColumn;
            this.yRow = dataOne.yRow;
            this.gridPointList = new GridPoint[this.xColumn, this.yRow];
            this.componentType = BattleViewComponentType.MAP;
            this.towerComponent = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            this.dataComponent = dataOne;
        }

        public override void Init()
        {
            if (this.IsBuilt)
            {
                return;
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridNormal, out this.sprGirdNoramlState))
            {
                Debug.LogError("[BVMapComponent] Grid Sprite 未预加载");
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridStart, out this.sprGirdStartState))
            {
                Debug.LogError("[BVMapComponent] StartSprite 未预加载");
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridCantBuild, out this.sprGirdCantBuildState))
            {
                Debug.LogError("[BVMapComponent] cantBuild Sprite 未预加载");
            }

            this.LoadMapGrid();
            this.AddListener();
            this.IsBuilt = this.gridPointList != null && this.HasAnyGridPoint();
        }

        bool HasAnyGridPoint()
        {
            if (this.gridPointList == null)
            {
                return false;
            }

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    if (this.gridPointList[x, y] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override void ResetRound(BattleViewResetPass pass)
        {
            if (pass != BattleViewResetPass.AfterModel)
            {
                return;
            }

            this.RefreshBattleBindings();
            this.towerComponent = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            this.dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);

            this.pendingRemoveGridX = -1;
            this.pendingRemoveGridY = -1;
            this.UnregisterAllTowerLevelUpListeners();
            this.RemoveListener();

            if (this.gridPointList != null)
            {
                for (int x = 0; x < this.xColumn; x++)
                {
                    for (int y = 0; y < this.yRow; y++)
                    {
                        GridPoint gridPoint = this.gridPointList[x, y];
                        if (gridPoint == null)
                        {
                            continue;
                        }

                        gridPoint.InitInfo(x, y);
                        gridPoint.ResetRound();
                    }
                }
            }

            this.AddListener();
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.OnCoinChange);
            this.eventDispatcher.AddListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.OnBattleUnitAdd);
            this.eventDispatcher.AddListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.OnBattleUnitRemove);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.OnCoinChange);
            this.eventDispatcher.RemoveListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.OnBattleUnitAdd);
            this.eventDispatcher.RemoveListener<string, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.OnBattleUnitRemove);
            this.UnregisterAllTowerLevelUpListeners();
        }

        private void RegisterTowerLevelUpListener(BattleUnit_Tower tower)
        {
            if (tower == null || tower.eventDipatcher == null)
            {
                return;
            }

            if (!this.levelUpSignalTowers.Add(tower))
            {
                return;
            }

            tower.eventDipatcher.AddListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.OnTowerLevelUp);
        }

        private void UnregisterTowerLevelUpListener(BattleUnit_Tower tower)
        {
            if (tower == null || tower.eventDipatcher == null)
            {
                return;
            }

            if (!this.levelUpSignalTowers.Remove(tower))
            {
                return;
            }

            tower.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.OnTowerLevelUp);
        }

        private void UnregisterAllTowerLevelUpListeners()
        {
            foreach (BattleUnit_Tower tower in this.levelUpSignalTowers)
            {
                if (tower != null && tower.eventDipatcher != null)
                {
                    tower.eventDipatcher.RemoveListener<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this.OnTowerLevelUp);
                }
            }

            this.levelUpSignalTowers.Clear();
        }

        private void OnCoinChange(int coin)
        {
            this.RefreshAllLevelUpSignals();
            this.ApplyPendingRemoveLevelUpSignal();
        }

        private void OnBattleUnitAdd(string type, BattleUnit unit)
        {
            if (!type.Equals(BattleUnitType.TOWER))
            {
                return;
            }

            BattleUnit_Tower tower = unit as BattleUnit_Tower;
            if (tower == null)
            {
                return;
            }

            this.RegisterTowerLevelUpListener(tower);
            this.RefreshAllLevelUpSignals();
        }

        private void OnBattleUnitRemove(string type, BattleUnit unit)
        {
            if (!type.Equals(BattleUnitType.TOWER))
            {
                return;
            }

            BattleUnit_Tower tower = unit as BattleUnit_Tower;
            if (tower == null || this.gridPointList == null)
            {
                return;
            }

            this.UnregisterTowerLevelUpListener(tower);

            this.pendingRemoveGridX = tower.x;
            this.pendingRemoveGridY = tower.y;

            GridPoint gridPoint = this.gridPointList[tower.x, tower.y];
            if (gridPoint != null)
            {
                gridPoint.SetLevelUpSignalVisible(false);
            }
        }

        private void ApplyPendingRemoveLevelUpSignal()
        {
            if (this.pendingRemoveGridX < 0 || this.pendingRemoveGridY < 0 || this.gridPointList == null)
            {
                return;
            }

            GridPoint gridPoint = this.gridPointList[this.pendingRemoveGridX, this.pendingRemoveGridY];
            if (gridPoint != null)
            {
                gridPoint.SetLevelUpSignalVisible(false);
            }

            this.pendingRemoveGridX = -1;
            this.pendingRemoveGridY = -1;
        }

        private void OnTowerLevelUp(BattleUnit_Tower tower)
        {
            this.RefreshAllLevelUpSignals();
        }

        private void RefreshAllLevelUpSignals()
        {
            if (this.gridPointList == null || this.towerComponent == null || this.dataComponent == null)
            {
                return;
            }

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    GridPoint gridPoint = this.gridPointList[x, y];
                    if (gridPoint == null)
                    {
                        continue;
                    }

                    gridPoint.SetLevelUpSignalVisible(this.CanShowLevelUpSignal(x, y));
                }
            }
        }

        private bool CanShowLevelUpSignal(int x, int y)
        {
            BattleUnit_Tower tower = this.towerComponent.GetTowerInfo(x, y);
            if (tower == null || tower.isMaxLevel)
            {
                return false;
            }

            return this.dataComponent.CoinCount >= tower.price[tower.curLevel + 1];
        }

        private void LoadMapGrid()
        {
            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVMapComponent] BVSceneComponent 未注册，跳过格子创建。");
                return;
            }

            GameObject gridList = scene.RegisterGameContainer("GridContainer");
            if (gridList == null)
            {
                Debug.LogError("[BVMapComponent] GridContainer 未就绪，跳过格子创建。");
                return;
            }

            GameObject item;
            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.Grid,
                out item))
            {
                Debug.LogError("[BVMapComponent] Grid 预制体未预加载");
                return;
            }

            BattleMapComponent mapComponent = (BattleMapComponent)this.battle.GetComponent(BattleComponentType.MapComponent);
            BattleMapGrid[,] mapGridInfo = mapComponent.gridsList;

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    GameObject itemGo = GameObject.Instantiate(item);
                    itemGo.transform.position = new Vector3((float)mapGridInfo[x, y].realX, (float)mapGridInfo[x, y].realY, 0);
                    itemGo.transform.SetParent(gridList.transform);
                    GridPoint gridPoint = itemGo.GetComponent<GridPoint>();
                    if (gridPoint == null)
                    {
                        Debug.LogError("[BVMapComponent] Grid 预制体缺少 GridPoint 组件");
                        continue;
                    }

                    this.gridPointList[x, y] = gridPoint;
                    gridPoint.InitTrans(this.battleView);
                    gridPoint.InitInfo(x, y);
                }
            }
        }

        public override void Start()
        {
            if (this.gridPointList == null)
            {
                return;
            }

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    GridPoint gridPoint = this.gridPointList[x, y];
                    if (gridPoint == null)
                    {
                        continue;
                    }

                    gridPoint.StartGame();
                }
            }
        }

        public override void ClearGameInfo()
        {
            this.RemoveListener();
            this.UnregisterAllTowerLevelUpListeners();
            this.pendingRemoveGridX = -1;
            this.pendingRemoveGridY = -1;
            this.IsBuilt = false;

            if (this.gridPointList != null)
            {
                for (int x = 0; x < this.xColumn; x++)
                {
                    for (int y = 0; y < this.yRow; y++)
                    {
                        this.gridPointList[x, y] = null;
                    }
                }
            }

            base.ClearGameInfo();
        }

    }
}
