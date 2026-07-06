using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 经典 PVE 地图：从 <see cref="PveModelBattleParams.LevelInfo"/> 加载关卡格子、怪物路径与建造规则。
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
            if (!this.IsGridInBounds(x, y))
            {
                return false;
            }

            BattleMapGrid grid = this.gridsList[x, y];
            return grid.state.canBuild && !grid.hasTower;
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
