using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 防御塔建造/升级/拆除与每帧 Tick。Tick 开头调用 HitTest.AssignTowerFocusTargets，再驱动各塔索敌与开火。
    /// </summary>
    public class BattleTowerComponent : BaseBattleComponent
    {
        public Dictionary<int, BattleUnit_Tower> curTowerDic = new Dictionary<int, BattleUnit_Tower>();
        //这个int不是tower的uid，是根据坐标换算得到的

        protected BattleDataComponent dataComponent;
        protected BattleMapComponent mapComponent;
        public int[] canBuildTowerList { get; protected set; } //可以建造塔的id
        public int canBuildTowerListLength { get; protected set; }

        public BattleTowerComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.TowerComponent;
            PveModelBattleParams launchParams = this.baseBattle.LaunchParams;
            if (launchParams?.Stage != null)
            {
                this.canBuildTowerList = launchParams.Stage.mTowerIDList;
                this.canBuildTowerListLength = launchParams.Stage.mTowerIDListLength;
            }
            else
            {
                this.canBuildTowerList = new int[] { 1, 2, 3, 4 };
                this.canBuildTowerListLength = this.canBuildTowerList.Length;
            }
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

        public bool IsHaveTower(int x, int y) //地图模块用
        {
            return this.curTowerDic.ContainsKey(this.GetExChangeInt(x, y));
        }

        public void ExePlayerOrder(InputOrder order)
        {
            if (order.order == InputOrderType.ADD_ORDER)
            {
                int gridKey = this.GetExChangeInt(order.x, order.y);
                if (this.curTowerDic.ContainsKey(gridKey))
                {
                    Debug.LogWarning(String.Format("该格子已有防御塔，忽略重复建造: {0}", gridKey));
                    return;
                }

                if (!this.mapComponent.IsCanBuildTower(order.x, order.y))
                {
                    Debug.LogWarning(String.Format("该格子不可建造防御塔: ({0},{1})", order.x, order.y));
                    return;
                }

                int price = (int)(TowerConfigReader.Instance.GetSingleTowerConfig(order.towerId)["price0"]);
                if (price > dataComponent.CoinCount)
                {
                    this.baseBattle.HostBridge?.ShowInsufficientGoldTip();
                    return;
                }
                BattleUnit_Tower tower = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Tower>(BattleUnitType.TOWER);
                if (tower == null)
                {
                    tower = new BattleUnit_Tower(this.baseBattle);
                }
                Fix64Vector2 birthPoint = mapComponent.GetMapGridPosition(order.x, order.y);
                tower.LoadInfo(this.baseBattle.GetUid(), TowerConfigReader.Instance.GetSingleTowerConfig(order.towerId), birthPoint);
                tower.LoadInfo1(order.x, order.y);
                tower.Init();
                tower.InitComponents();
                this.curTowerDic.Add(gridKey, tower);
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

        /// <summary>集火分配 → 各塔 <see cref="BattleUnit_Tower.OnTick"/>（见 BattleCombatFlow.md）。</summary>
        public override void OnTick(Fix64 time)
        {
            BattleSimpleHitTestComponent hitTest =
                (BattleSimpleHitTestComponent)this.baseBattle.GetComponent(BattleComponentType.HitTestComponent);
            if (hitTest != null)
            {
                hitTest.AssignTowerFocusTargets();
            }

            foreach (KeyValuePair<int, BattleUnit_Tower> info in this.curTowerDic)
            {
                info.Value.OnTick(time);
            }

        }

        public BattleUnit_Tower GetTowerInfo(int x, int y)
        {
            int id = this.GetExChangeInt(x, y);
            BattleUnit_Tower tower;
            if (this.curTowerDic.TryGetValue(id, out tower))
            {
                return tower;
            }

            return null;
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            foreach (KeyValuePair<int, BattleUnit_Tower> info in this.curTowerDic)
            {
                info.Value.ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.TOWER, info.Value);
            }
            this.curTowerDic.Clear();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }

    }
}
