using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVMapComponent : BaseBattleViewComponent
    {
        private readonly List<AssetLoadHandle> _prefabHandles = new List<AssetLoadHandle>();
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
            this.sprGirdNoramlState = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Grid");
            this.sprGirdStartState = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/StartSprite");
            this.sprGirdCantBuildState = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/cantBuild");
            this.LoadMapGrid();
        }

        private void LoadMapGrid()
        {
            GameObject item = GameObjectResourceManager.Instance.LoadPrefabBlocking(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.Grid, out AssetLoadHandle h);
            if (h.IsValid)
            {
                _prefabHandles.Add(h);
            }

            if (item == null)
            {
                Debug.LogError("[BVMapComponent] Grid 预制体加载失败");
                return;
            }

            BattleMapComponent mapComponent = (BattleMapComponent)this.battle.GetComponent(BattleComponentType.MapComponent);
            BattleMapGrid[,] mapGridInfo = mapComponent.gridsList;

            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            GameObject gridList = scene.RegisterGameContainer("GridContainer");

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    GameObject itemGo = GameObject.Instantiate(item);
                    itemGo.transform.position = new Vector3((float)mapGridInfo[x, y].realX, (float)mapGridInfo[x, y].realY, 0);
                    itemGo.transform.SetParent(gridList.transform);
                    this.gridPointList[x, y] = itemGo.transform.GetComponent<GridPoint>();
                    this.gridPointList[x, y].InitTrans(this.battleView);
                    this.gridPointList[x, y].InitInfo(x, y);
                }
            }
        }


        public override void Start()
        {
            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    this.gridPointList[x, y].StartGame();
                }
            }
        }

        public override void ClearGameInfo()
        {
            for (int i = 0; i < _prefabHandles.Count; i++)
            {
                _prefabHandles[i].Dispose();
            }

            _prefabHandles.Clear();
            base.ClearGameInfo();
        }

    }
}
