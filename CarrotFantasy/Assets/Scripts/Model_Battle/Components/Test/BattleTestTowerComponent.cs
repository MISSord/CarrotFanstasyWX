namespace CarrotFantasy
{
    /// <summary>测试战斗用塔组件：可建造列表来自 <see cref="BattleTestDataComponent"/>，不依赖 <see cref="BattleParamServer.curStage"/>。</summary>
    public class BattleTestTowerComponent : BattleTowerComponent
    {
        public BattleTestTowerComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.TowerComponent;
        }

        public override void Init()
        {
            BattleTestDataComponent data = this.baseBattle.GetComponent(BattleComponentType.DataComponent) as BattleTestDataComponent;
            if (data != null && data.curTowerIDList != null && data.curTowerIDList.Length > 0)
            {
                this.canBuildTowerList = data.curTowerIDList;
                this.canBuildTowerListLength = data.towerIDListLength;
            }
            else
            {
                this.canBuildTowerList = new int[] { 1, 2, 3, 4 };
                this.canBuildTowerListLength = this.canBuildTowerList.Length;
            }

            this.dataComponent = (BattleDataComponent)this.baseBattle.GetComponent(BattleComponentType.DataComponent);
            this.mapComponent = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
        }
    }
}
