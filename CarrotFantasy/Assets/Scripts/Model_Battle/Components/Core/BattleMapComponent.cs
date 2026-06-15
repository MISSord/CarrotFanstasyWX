namespace CarrotFantasy
{
    /// <summary>
    /// 战斗地图基类：格子网格、世界坐标与地图边界。
    /// 关卡数据与怪物路径见 <see cref="BattlePVEMapComponent"/> / <see cref="BattleTestMapComponent"/>。
    /// </summary>
    public class BattleMapComponent : BaseBattleComponent
    {
        public int xColumn { get; protected set; }

        public int yRow { get; protected set; }

        public BattleMapGrid[,] gridsList { get; protected set; }

        public Fix64Vector2 mapLeftBottomPosition { get; protected set; }

        public Fix64Vector2 mapRightTopPosition { get; protected set; }

        public BattleMapComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.MapComponent;
        }

        public override void Init()
        {
            this.InitGridSizeFromData();
            this.LoadMapGrid();
        }

        protected void InitGridSizeFromData()
        {
            BattleDataComponent dataOne = (BattleDataComponent)this.baseBattle.GetComponent(BattleComponentType.DataComponent);
            this.xColumn = dataOne.xColumn;
            this.yRow = dataOne.yRow;
            this.gridsList = new BattleMapGrid[this.xColumn, this.yRow];
        }

        protected void LoadMapGrid()
        {
            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    this.gridsList[x, y] = new BattleMapGrid(this.baseBattle.GetUid(), x, y);
                }
            }
        }

        protected void ComputeMapWorldBounds()
        {
            BattleMapLayoutUtil.ComputeMapWorldBounds(
                this.gridsList,
                this.xColumn,
                this.yRow,
                out Fix64Vector2 leftBottom,
                out Fix64Vector2 rightTop);
            this.mapLeftBottomPosition = leftBottom;
            this.mapRightTopPosition = rightTop;
        }

        public virtual bool IsCanBuildTower(int x, int y)
        {
            return this.IsGridInBounds(x, y);
        }

        protected bool IsGridInBounds(int x, int y)
        {
            return x >= 0 && x < this.xColumn && y >= 0 && y < this.yRow;
        }

        public Fix64Vector2 GetMapGridPosition(int x, int y)
        {
            return new Fix64Vector2(this.gridsList[x, y].realX, this.gridsList[x, y].realY);
        }

        public void ExePlayerOrder(InputOrder order)
        {
            if (!this.IsGridInBounds(order.x, order.y))
            {
                return;
            }

            BattleTowerComponent towerComponent =
                (BattleTowerComponent)this.baseBattle.GetComponent(BattleComponentType.TowerComponent);
            bool hasTower = towerComponent != null && towerComponent.IsHaveTower(order.x, order.y);
            this.gridsList[order.x, order.y].ChangeTowerState(hasTower);
        }

        public override void ClearInfo()
        {
            this.gridsList = null;
        }
    }
}
