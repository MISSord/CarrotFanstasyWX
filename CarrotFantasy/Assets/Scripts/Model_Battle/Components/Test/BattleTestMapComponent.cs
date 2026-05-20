using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 碰撞/性能测试用地图：不加载关卡 JSON，全图可建造、无道具，合成测试用路径。
    /// </summary>
    public class BattleTestMapComponent : BattleMapComponent, IBattleMapLevelData
    {
        public LevelInfo LevelInfo { get; private set; }

        public List<Fix64Vector2> monsterPathList;

        public Fix64Vector2 startPoint { get; private set; }

        LevelInfo IBattleMapLevelData.LevelInfo
        {
            get { return this.LevelInfo; }
        }

        public BattleTestMapComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public override void Init()
        {
            this.InitGridSizeFromData();
            this.BuildSyntheticLevelInfo();
            this.LoadMapGrid();
            this.BuildSyntheticPathAndBounds();
        }

        private void BuildSyntheticLevelInfo()
        {
            this.LevelInfo = new LevelInfo();
            this.LevelInfo.bigLevelID = 1;
            this.LevelInfo.levelID = 1;
            this.LevelInfo.gridPoints = new List<BattleMapGrid.GridState>();
            this.LevelInfo.monsterPath = new List<BattleMapGrid.GridIndex>();
            this.LevelInfo.roundInfo = new List<Round.RoundInfo>();

            for (int x = 0; x < this.xColumn; x++)
            {
                for (int y = 0; y < this.yRow; y++)
                {
                    BattleMapGrid.GridState state = new BattleMapGrid.GridState();
                    state.canBuild = true;
                    state.isMonsterPoint = false;
                    state.hasItem = false;
                    state.itemID = 0;
                    this.LevelInfo.gridPoints.Add(state);
                }
            }
        }

        private void BuildSyntheticPathAndBounds()
        {
            for (int x = 0; x < this.xColumn; x++)
            {
                BattleMapGrid.GridIndex idx = new BattleMapGrid.GridIndex();
                idx.xIndex = x;
                idx.yIndex = 0;
                this.LevelInfo.monsterPath.Add(idx);
            }

            for (int y = 0; y < this.yRow; y++)
            {
                BattleMapGrid.GridIndex idx = new BattleMapGrid.GridIndex();
                idx.xIndex = this.xColumn - 1;
                idx.yIndex = y;
                this.LevelInfo.monsterPath.Add(idx);
            }

            BattleMapLayoutUtil.ApplyGridStates(this.gridsList, this.xColumn, this.yRow, this.LevelInfo);
            this.monsterPathList = BattleMapLayoutUtil.BuildMonsterPathWorldPositions(
                this.gridsList,
                this.xColumn,
                this.yRow,
                this.LevelInfo,
                out Fix64Vector2 start);
            this.startPoint = start;
            this.ComputeMapWorldBounds();
        }

        public override bool IsCanBuildTower(int x, int y)
        {
            return this.IsGridInBounds(x, y);
        }

        public override void ClearInfo()
        {
            if (this.monsterPathList != null)
            {
                this.monsterPathList.Clear();
            }

            this.LevelInfo = null;
            base.ClearInfo();
        }
    }
}
