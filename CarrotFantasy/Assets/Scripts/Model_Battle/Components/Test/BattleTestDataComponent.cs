namespace CarrotFantasy
{
    /// <summary>
    /// 碰撞/性能测试用数据：不读关卡、波次、萝卜与 <see cref="PveModelBattleParams"/>。
    /// </summary>
    public class BattleTestDataComponent : BattleDataComponent
    {
        /// <summary>测试怪物显示用章节 id（<see cref="BattleUnit_Monster.LoadInfo2"/>）。</summary>
        public int monsterConfigBigLevel { get; private set; }

        public BattleTestDataComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public override void Init()
        {
            int xc = 24;
            int yr = 16;
            if (BattleParamServer.Instance != null)
            {
                if (BattleParamServer.Instance.hitTestMapXColumn > 0)
                {
                    xc = BattleParamServer.Instance.hitTestMapXColumn;
                }

                if (BattleParamServer.Instance.hitTestMapYRow > 0)
                {
                    yr = BattleParamServer.Instance.hitTestMapYRow;
                }
            }

            this.xColumn = xc;
            this.yRow = yr;
            this.CoinCount = 99999;
            this.monsterConfigBigLevel = 1;
            this.curTowerIDList = new int[] { 1, 2, 3, 4 };
            this.towerIDListLength = this.curTowerIDList.Length;
            this.RegisterCoinListener();
        }
    }
}
