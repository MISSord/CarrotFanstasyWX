using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleTowerComponent : BaseBattleComponent
    {
        public Dictionary<int, BattleUnit_Tower> curTowerDic = new Dictionary<int, BattleUnit_Tower>();
        //这个int不是tower的uid，是根据坐标换算得到的

        private BattleDataComponent dataComponent;
        private BattleMapComponent mapComponent;
        public int[] canBuildTowerList { get; private set; } //可以建造塔的id
        public int canBuildTowerListLength { get; private set; }

        public BattleTowerComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.TowerComponent;
            this.canBuildTowerList = BattleParamServer.Instance.curStage.mTowerIDList;
            this.canBuildTowerListLength = BattleParamServer.Instance.curStage.mTowerIDListLength;
        }

        public override void Init()
        {
            this.dataComponent = (BattleDataComponent)this.baseBattle.GetComponent(BattleComponentType.DataComponent);
            this.mapComponent = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
        }

        private int GetExChangeInt(int x, int y)
        {
            return x * 100 + y;
        }

        public bool isHaveTower(int x, int y) //地图模块用
        {
            return this.curTowerDic.ContainsKey(this.GetExChangeInt(x, y));
        }

        public void ExePlayerOrder(InputOrder order)
        {
            if (order.order == InputOrderType.ADD_ORDER)
            {
                int price = (int)(TowerConfigReader.Instance.GetSingleTowerConfig(order.towerId)["price0"]);
                if (price > dataComponent.CoinCount)
                {
                    UIServer.Instance.ShowTip(LanguageUtil.Instance.GetString(1004));
                    return;
                }
                BattleUnit_Tower tower = BattleUnitPool.Instance.getNewBattleUnit<BattleUnit_Tower>(BattleUnitType.TOWER);
                if (tower == null)
                {
                    tower = new BattleUnit_Tower(this.baseBattle);
                }
                Fix64Vector2 birthPoint = mapComponent.GetMapGridPosition(order.x, order.y);
                tower.LoadInfo(this.baseBattle.GetUid(), TowerConfigReader.Instance.GetSingleTowerConfig(order.towerId), birthPoint);
                tower.LoadInfo1(order.x, order.y);
                tower.Init();
                tower.InitComponents();
                this.curTowerDic.Add(this.GetExChangeInt(order.x, order.y), tower);
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.TOWER, tower);
                this.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, -tower.price[tower.curLevel]);
            }
            else if (order.order == InputOrderType.UPDATE_ORDER)
            {
                BattleUnit_Tower tower;
                int id = this.GetExChangeInt(order.x, order.y);
                if (this.curTowerDic.TryGetValue(id, out tower))
                {
                    if (tower.isMaxLevel == true) return;
                    if (dataComponent.CoinCount >= tower.price[tower.curLevel + 1])
                    {
                        this.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, -tower.price[tower.curLevel]);
                        tower.UpdateLevel();
                    }
                }
                else
                {
                    Debug.Log(String.Format("执行升级操作失败，没有{0}塔", id));
                }
            }
            else if (order.order == InputOrderType.REMOVE_ORDER)
            {
                BattleUnit_Tower tower;
                int id = this.GetExChangeInt(order.x, order.y);
                if (this.curTowerDic.TryGetValue(id, out tower))
                {
                    this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.TOWER, tower);
                    tower.ClearInfo();
                    BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.TOWER, tower);
                    this.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, tower.price[tower.curLevel] - 20);
                    this.curTowerDic.Remove(this.GetExChangeInt(order.x, order.y));
                }
                else
                {
                    Debug.Log(String.Format("执行移除操作失败，没有{0}塔", id));
                }
            }
        }

        public override void OnTick(Fix64 time)
        {
            foreach (KeyValuePair<int, BattleUnit_Tower> info in this.curTowerDic)
            {
                info.Value.OnTick(time);
            }

        }

        public BattleUnit_Tower GetTowerInfo(int x, int y)
        {
            int id = this.GetExChangeInt(x, y);
            if (this.curTowerDic.ContainsKey(id))
            {
                return this.curTowerDic[id];
            }
            else
            {
                Debug.Log(String.Format("视图层获取防御塔信息失败，没有{0}塔", id));
            }
            return null;
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            foreach (KeyValuePair<int, BattleUnit_Tower> info in this.curTowerDic)
            {
                info.Value.ClearInfo();
            }
            this.curTowerDic.Clear();
        }

        public override void Dispose()
        {
            this.ClearInfo();
        }

    }
}
