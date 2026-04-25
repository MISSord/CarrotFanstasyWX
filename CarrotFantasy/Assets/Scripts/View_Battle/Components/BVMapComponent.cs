using UnityEngine;

namespace CarrotFantasy
{
    public class BVMapComponent : BaseBattleViewComponent
    {
        public Sprite sprGirdNoramlState;
        public Sprite sprGirdStartState;
        public Sprite sprGirdCantBuildState;

        public GridPoint[,] gridPointList;
        private GameObject rootGameObject;

        private GameObject node_bg;

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
            this.loadMapGrid();
        }

        private void loadMapGrid()
        {
            GameObject item = ResourceLoader.Instance.getGameObject("Prefabs/Game/Grid");

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
            base.ClearGameInfo();
        }

    }
}
