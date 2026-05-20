using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗数据基类：局内金币、本场地图格子、可建造塔列表（各玩法共用）。
    /// PVE 章节/波次/萝卜/结算见 <see cref="BattlePVEDataComponent"/>。
    /// </summary>
    public class BattleDataComponent : BaseBattleComponent
    {
        public int CoinCount { get; protected set; }

        public int[] curTowerIDList { get; protected set; }

        public int towerIDListLength { get; protected set; }

        public int yRow { get; protected set; }

        public int xColumn { get; protected set; }

        public BattleDataComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.DataComponent;
        }

        public override void Init()
        {
            this.InitDefaults();
            this.RegisterCoinListener();
        }

        protected virtual void InitDefaults()
        {
            this.CoinCount = 800;
            this.xColumn = 12;
            this.yRow = 8;
        }

        protected void RegisterCoinListener()
        {
            this.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.HandleCoinCountChange);
        }

        protected void UnregisterCoinListener()
        {
            this.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.HandleCoinCountChange);
        }

        protected virtual void HandleCoinCountChange(int change)
        {
            if (this.CoinCount + change >= 0)
            {
                this.CoinCount += change;
            }
            else
            {
                Debug.LogError(String.Format("数量扣除不合法，原{0},改变{1}", this.CoinCount, change));
            }
        }

        public override void ClearInfo()
        {
            this.UnregisterCoinListener();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
