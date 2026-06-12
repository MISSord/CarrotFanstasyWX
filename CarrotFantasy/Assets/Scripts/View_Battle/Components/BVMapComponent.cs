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

        public BVMapComponent(BattleView_base battleView) : base(battleView)
        {
            BattleDataComponent dataOne = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.xColumn = dataOne.xColumn;
            this.yRow = dataOne.yRow;
            this.gridPointList = new GridPoint[this.xColumn, this.yRow];
            this.componentType = BattleViewComponentType.MAP;
        }

        public override void Init()
        {
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

            int created = 0;
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
                    created++;
                }
            }

            BattleFlowLog.Step(
                "LoadMapGrid",
                "columns=" + this.xColumn +
                " rows=" + this.yRow +
                " created=" + created +
                " GridContainer#" + gridList.GetInstanceID());
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
