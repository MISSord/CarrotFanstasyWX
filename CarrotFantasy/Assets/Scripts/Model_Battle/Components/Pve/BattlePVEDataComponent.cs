namespace CarrotFantasy
{
    /// <summary>
    /// 经典 / 流场 PVE 专用数据：章节关卡、波次、萝卜、胜负结算。
    /// </summary>
    public class BattlePVEDataComponent : BattleDataComponent
    {
        public int curWaves { get; private set; }
        public int totalWaves { get; private set; }
        public int bigLevel { get; private set; }
        public int level { get; private set; }
        public int carrotLive { get; private set; }

        public BattlePVEDataComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public static BattlePVEDataComponent GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.DataComponent) as BattlePVEDataComponent;
        }

        public override void Init()
        {
            base.Init();

            PveModelBattleParams launchParams = BattleParamAccess.Current;
            if (launchParams != null)
            {
                this.bigLevel = launchParams.BigLevelId;
                this.level = launchParams.LevelId;

                LevelInfo levelInfo = launchParams.LevelInfo;
                this.totalWaves = LevelWaveQuery.GetTotalWaves(levelInfo);
                if (this.totalWaves <= 0 && launchParams.Stage != null)
                {
                    this.totalWaves = launchParams.Stage.mTotalRound;
                }

                if (launchParams.Stage != null)
                {
                    this.curTowerIDList = launchParams.Stage.mTowerIDList;
                    this.towerIDListLength = this.curTowerIDList != null ? this.curTowerIDList.Length : 0;
                }
            }

            this.curWaves = 0;
            this.carrotLive = 10;
            this.RegisterCarrotListener();
        }

        private void RegisterCarrotListener()
        {
            this.eventDispatcher.AddListener(BattleEvent.CARROT_LIVE_REDUCE, this.CarrotLiveReduce);
        }

        private void UnregisterCarrotListener()
        {
            this.eventDispatcher.RemoveListener(BattleEvent.CARROT_LIVE_REDUCE, this.CarrotLiveReduce);
        }

        public void WavesNumberChange()
        {
            this.curWaves += 1;
            this.eventDispatcher.DispatchEvent<int>(BattleEvent.WAVES_NUMBER_ADD, this.curWaves);
        }

        private void CarrotLiveReduce()
        {
            this.carrotLive -= 1;
        }

        public bool CarrotIsDead()
        {
            return this.carrotLive <= 0;
        }

        public void GameOverByCarrotDead()
        {
            this.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            PveMatchSettlement settlement = new PveMatchSettlement();
            settlement.IsVictory = false;
            settlement.VictoryProgress = null;
            this.baseBattle.eventDispatcher.DispatchEvent(BattleCoreEvent.PVE_MATCH_SETTLED, settlement);
        }

        public void GameOverByMonsterDead()
        {
            SingleMapInfo unSaveMapInfo = new SingleMapInfo();
            unSaveMapInfo.bigLevelId = (byte)this.bigLevel;
            unSaveMapInfo.levelId = (byte)this.level;

            BattleItemComponent itemComponent = (BattleItemComponent)this.baseBattle.GetComponent(BattleComponentType.ItemComponent);
            if (itemComponent != null && itemComponent.battleItemList.Count == 0)
            {
                unSaveMapInfo.isAllClear = MapInfoType.ALL_CLEAR;
            }
            else
            {
                unSaveMapInfo.isAllClear = MapInfoType.NOT_ALL_CLEAR;
            }

            unSaveMapInfo.carrotState = (byte)this.CarrotTropyLevel();
            unSaveMapInfo.unLocked = MapInfoType.UNLOCK_LEVEL;

            this.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.PAUSE_THE_GAME);
            PveMatchSettlement settlement = new PveMatchSettlement();
            settlement.IsVictory = true;
            settlement.VictoryProgress = unSaveMapInfo;
            this.baseBattle.eventDispatcher.DispatchEvent(BattleCoreEvent.PVE_MATCH_SETTLED, settlement);
        }

        public int CarrotTropyLevel()
        {
            if (this.carrotLive >= 7)
            {
                return 3;
            }

            if (this.carrotLive >= 3)
            {
                return 2;
            }

            return 1;
        }

        public override void ClearInfo()
        {
            this.UnregisterCarrotListener();
            base.ClearInfo();
        }
    }
}
