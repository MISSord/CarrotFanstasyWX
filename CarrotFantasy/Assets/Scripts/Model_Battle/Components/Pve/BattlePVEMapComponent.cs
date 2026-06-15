using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 经典 / 流场 PVE 地图：从 <see cref="PveModelBattleParams.LevelInfo"/> 加载关卡格子、怪物路径与建造规则。
    /// </summary>
    public class BattlePVEMapComponent : BattleMapComponent, IBattleMapLevelData
    {
        public LevelInfo LevelInfo { get; private set; }

        public List<Fix64Vector2> monsterPathList;

        public Fix64Vector2 startPoint { get; private set; }

        LevelInfo IBattleMapLevelData.LevelInfo
        {
            get { return this.LevelInfo; }
        }

        public BattlePVEMapComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public static BattlePVEMapComponent GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.MapComponent) as BattlePVEMapComponent;
        }

        public override void Init()
        {
            this.InitGridSizeFromData();
            PveModelBattleParams launchParams = this.baseBattle.LaunchParams;
            this.LevelInfo = launchParams != null ? launchParams.LevelInfo : null;
            this.LoadMapGrid();
            this.LoadLevelMapInfo();
        }

        private void LoadLevelMapInfo()
        {
            if (this.LevelInfo == null)
            {
                return;
            }

            this.monsterPathList = BattleMapLayoutUtil.BuildMonsterPathWorldPositions(
                this.gridsList,
                this.xColumn,
                this.yRow,
                this.LevelInfo,
                out Fix64Vector2 start);
            this.startPoint = start;
            BattleMapLayoutUtil.ApplyGridStates(this.gridsList, this.xColumn, this.yRow, this.LevelInfo);
            this.ComputeMapWorldBounds();
        }

        public override bool IsCanBuildTower(int x, int y)
        {
            if (!this.IsGridInBounds(x, y) || this.LevelInfo == null || this.LevelInfo.gridPoints == null)
            {
                return false;
            }

            int index = this.GetListNumber(x, y);
            if (index < 0 || index >= this.LevelInfo.gridPoints.Count)
            {
                return false;
            }

            return this.LevelInfo.gridPoints[index].canBuild;
        }

        /// <summary>与视图层格子编号一致的扁平索引（1-based 坐标换算，保持与原 PVE 逻辑一致）。</summary>
        private int GetListNumber(int x, int y)
        {
            return (x - 1) * this.xColumn + (y - 1) * this.yRow;
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
