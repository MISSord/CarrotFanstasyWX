using System;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CarrotFantasy
{
    /// <summary>
    /// 子弹/怪物/物品的圆-圆碰撞与玩家集火分配。
    /// 碰撞 broad phase 用 <see cref="BattleSpatialGrid"/>，每帧仅在 <see cref="OnTick"/> 开头 <see cref="RefreshSpatialGrid"/> 一次。
    /// 集火 <see cref="AssignTowerFocusTargets"/> 在 Tower 组件阶段调用，遍历全部塔，不依赖网格。详见 BattleCombatFlow.md。
    /// </summary>
    public class BattleSimpleHitTestComponent : BaseBattleComponent, IHitTestPerfStats
    {
        private Dictionary<string, List<BattleUnit>> registerUnitDic = new Dictionary<string, List<BattleUnit>>();
        private Dictionary<string, List<UnitTransformComponent>> registerHitTestShapeDic = new Dictionary<string, List<UnitTransformComponent>>();

        private Dictionary<BattleUnit, List<BattleUnit>> curShouldCallBackDic = new Dictionary<BattleUnit, List<BattleUnit>>();

        private BattleUnit targetUnit = null;

        private BattleSpatialGrid spatialGrid;
        private readonly List<UnitTransformComponent> queryCandidates = new List<UnitTransformComponent>(32);
        private readonly HashSet<int> querySeenUids = new HashSet<int>();

        private readonly Stopwatch tickStopwatch = new Stopwatch();

        public string ModeName
        {
            get { return "SpatialGrid"; }
        }

        public long LastTickElapsedTicks { get; private set; }

        public int LastNarrowPhaseCount { get; private set; }

        public int LastBroadPhasePairCount { get; private set; }

        public long AccumulatedElapsedTicks { get; private set; }

        public int SampleFrameCount { get; private set; }

        public BattleSimpleHitTestComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.HitTestComponent;
        }

        public void ResetAccumulators()
        {
            this.AccumulatedElapsedTicks = 0;
            this.SampleFrameCount = 0;
        }

        public override void Init()
        {
            this.RegisterList(BattleUnitType.BULLET);
            this.RegisterList(BattleUnitType.MONSTER);
            this.RegisterList(BattleUnitType.TOWER);
            this.RegisterList(BattleUnitType.ITEM);

            this.AddListener();
            this.EnsureSpatialGrid();
        }

        /// <summary>地图 Init 完成后由 <see cref="Init"/> 或首次 <see cref="OnTick"/> 调用。</summary>
        void EnsureSpatialGrid()
        {
            BattleMapComponent map = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
            if (map == null || map.gridsList == null)
            {
                return;
            }

            if (this.spatialGrid != null && HasValidMapBounds(map))
            {
                return;
            }

            Fix64 cellSize = new Fix64(BattleConfig.MAP_RATIO * 2f);
            this.spatialGrid = new BattleSpatialGrid(map, cellSize);
        }

        static bool HasValidMapBounds(BattleMapComponent map)
        {
            Fix64 spanX = map.mapRightTopPosition.X - map.mapLeftBottomPosition.X;
            Fix64 spanY = map.mapRightTopPosition.Y - map.mapLeftBottomPosition.Y;
            return spanX > Fix64.Zero && spanY > Fix64.Zero;
        }

        /// <summary>按当前 Transform 重建空间网格；仅碰撞 broad phase 需要，须在 HitTest.OnTick 内调用。</summary>
        void RefreshSpatialGrid()
        {
            this.EnsureSpatialGrid();
            if (this.spatialGrid == null)
            {
                return;
            }

            this.spatialGrid.Clear();
            this.spatialGrid.InsertAll(this.registerHitTestShapeDic);
        }

        private void RegisterList(String type)
        {
            if (!this.registerUnitDic.ContainsKey(type))
            {
                this.registerUnitDic.Add(type, new List<BattleUnit>());
                this.registerHitTestShapeDic.Add(type, new List<UnitTransformComponent>());
            }
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBattleUnit);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBattleUnit);
            this.eventDispatcher.AddListener<BattleUnit>(BattleEvent.TARGET_CHANGE, this.SetTarget);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewBattleUnit);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveBattleUnit);
            this.eventDispatcher.RemoveListener<BattleUnit>(BattleEvent.TARGET_CHANGE, this.SetTarget);
        }

        private void RegisterNewBattleUnit(String type, BattleUnit battle)
        {
            UnitBeHitComponent beHit = (UnitBeHitComponent)battle.GetComponent(UnitComponentType.BEHIT);
            if (beHit == null) return;
            if (!this.registerUnitDic.ContainsKey(type))
            {
                Debug.Log(String.Format("没有注册{0}的碰撞链表", type));
                return;
            }
            this.registerHitTestShapeDic[type].Add((UnitTransformComponent)battle.GetComponent(UnitComponentType.TRANSFORM));
            this.registerUnitDic[type].Add(battle);
            if (type.Equals(BattleUnitType.MONSTER) || type.Equals(BattleUnitType.ITEM))
            {
                this.curShouldCallBackDic[battle] = new List<BattleUnit>();
            }
        }

        private void RemoveBattleUnit(String type, BattleUnit battle)
        {
            UnitBeHitComponent beHit = (UnitBeHitComponent)battle.GetComponent(UnitComponentType.BEHIT);
            if (beHit == null) return;
            if (!this.registerUnitDic.ContainsKey(type))
            {
                Debug.Log(String.Format("移除{0}的碰撞信息失败", type));
                return;
            }
            this.registerHitTestShapeDic[type].Remove((UnitTransformComponent)battle.GetComponent(UnitComponentType.TRANSFORM));
            this.registerUnitDic[type].Remove(battle);
            if (this.curShouldCallBackDic.ContainsKey(battle))
            {
                this.curShouldCallBackDic.Remove(battle);
            }
            if (this.targetUnit == battle)
            {
                this.SetTarget(null);
            }
        }

        public override void OnTick(Fix64 time)
        {
            this.LastNarrowPhaseCount = 0;
            this.LastBroadPhasePairCount = 0;
            this.tickStopwatch.Restart();

            // 怪物/子弹本帧 OnTick 已结束；重建格子分桶，broad phase 与当前 Transform 一致。
            this.RefreshSpatialGrid();

            this.ChooseSingleBeHit(BattleUnitType.MONSTER, BattleUnitType.BULLET);
            this.ChooseSingleBeHit(BattleUnitType.ITEM, BattleUnitType.BULLET);

            this.ExeTheCallBack();

            this.tickStopwatch.Stop();
            this.LastTickElapsedTicks = this.tickStopwatch.ElapsedTicks;
            this.AccumulatedElapsedTicks += this.LastTickElapsedTicks;
            this.SampleFrameCount += 1;
        }

        private void ChooseSingleBeHit(String type1, String type2)
        {
            List<UnitTransformComponent> list1 = this.registerHitTestShapeDic[type1];
            for (int i = 0; i < list1.Count; i++)
            {
                UnitTransformComponent unit1 = list1[i];
                List<BattleUnit> callbackList;
                if (!this.curShouldCallBackDic.TryGetValue(unit1.unit, out callbackList))
                {
                    continue;
                }

                if (!ShouldReceiveHit(unit1.unit))
                {
                    continue;
                }

                if (this.spatialGrid != null)
                {
                    this.querySeenUids.Clear();
                    this.spatialGrid.QueryNearLayer(type2, unit1, this.queryCandidates, this.querySeenUids);
                    for (int j = 0; j < this.queryCandidates.Count; j++)
                    {
                        UnitTransformComponent unit2 = this.queryCandidates[j];
                        this.LastBroadPhasePairCount += 1;
                        this.LastNarrowPhaseCount += 1;
                        if (BattleSpatialGrid.TryNarrowPhaseCircleCircle(unit1.bodyHitTestShape, unit2.bodyHitTestShape))
                        {
                            callbackList.Add(unit2.unit);
                        }
                    }
                }
                else
                {
                    this.ChooseSingleBeHitBruteForce(type2, unit1, callbackList);
                }
            }
        }

        private void ChooseSingleBeHitBruteForce(String type2, UnitTransformComponent unit1, List<BattleUnit> callbackList)
        {
            List<UnitTransformComponent> list2 = this.registerHitTestShapeDic[type2];
            for (int j = 0; j < list2.Count; j++)
            {
                UnitTransformComponent unit2 = list2[j];
                this.LastBroadPhasePairCount += 1;
                this.LastNarrowPhaseCount += 1;
                if (BattleSpatialGrid.TryNarrowPhaseCircleCircle(unit1.bodyHitTestShape, unit2.bodyHitTestShape))
                {
                    callbackList.Add(unit2.unit);
                }
            }
        }

        /// <summary>
        /// 将玩家集火目标写入射程内塔的 <see cref="BattleUnit_Tower.targetUnit"/>。
        /// 由 <see cref="BattleTowerComponent"/> 在塔 OnTick 之前调用（早于 HitTest 与子弹移动）。
        /// </summary>
        public void AssignTowerFocusTargets()
        {
            List<UnitTransformComponent> towers;
            if (this.registerHitTestShapeDic.TryGetValue(BattleUnitType.TOWER, out towers))
            {
                for (int i = 0; i < towers.Count; i++)
                {
                    UnitTransformComponent towerTransform = towers[i];
                    if (towerTransform != null && towerTransform.unit is BattleUnit_Tower)
                    {
                        ((BattleUnit_Tower)towerTransform.unit).targetUnit = null;
                    }
                }
            }

            if (this.targetUnit == null)
            {
                return;
            }

            this.ChooseSingleBeHitForTarget();
        }

        /// <summary>集火：遍历全部塔并用当前碰撞圆做窄相位，不依赖空间网格（塔数量少，且本方法在 HitTest 之前调用）。</summary>
        private void ChooseSingleBeHitForTarget()
        {
            UnitTransformComponent targetTransform = (UnitTransformComponent)this.targetUnit.GetComponent(UnitComponentType.TRANSFORM);
            if (targetTransform == null)
            {
                return;
            }

            List<UnitTransformComponent> towers;
            if (!this.registerHitTestShapeDic.TryGetValue(BattleUnitType.TOWER, out towers))
            {
                return;
            }

            for (int i = 0; i < towers.Count; i++)
            {
                UnitTransformComponent towerTransform = towers[i];
                if (towerTransform == null || !(towerTransform.unit is BattleUnit_Tower))
                {
                    continue;
                }

                if (BattleRangeQuery.IsInRange(towerTransform, targetTransform))
                {
                    ((BattleUnit_Tower)towerTransform.unit).targetUnit = this.targetUnit;
                }
            }
        }

        private void ExeTheCallBack()
        {
            if (this.curShouldCallBackDic.Count == 0) return;
            foreach (KeyValuePair<BattleUnit, List<BattleUnit>> info in this.curShouldCallBackDic)
            {
                if (info.Value.Count == 0)
                {
                    continue;
                }

                if (!ShouldReceiveHit(info.Key))
                {
                    info.Value.Clear();
                    continue;
                }

                UnitBeHitComponent tranBeHit = (UnitBeHitComponent)info.Key.GetComponent(UnitComponentType.BEHIT);
                if (tranBeHit == null || tranBeHit.BeHitCallBack == null)
                {
                    info.Value.Clear();
                    continue;
                }

                for (int i = 0; i < info.Value.Count; i++)
                {
                    if (!ShouldReceiveHit(info.Key))
                    {
                        break;
                    }

                    UnitBeHitComponent beHit = (UnitBeHitComponent)info.Value[i].GetComponent(UnitComponentType.BEHIT);
                    if (beHit == null || beHit.BeHitCallBack == null)
                    {
                        continue;
                    }

                    beHit.BeHitCallBack(info.Key);
                    tranBeHit.BeHitCallBack(info.Value[i]);
                }
                info.Value.Clear();
            }
        }

        static bool ShouldReceiveHit(BattleUnit receiver)
        {
            if (receiver == null)
            {
                return false;
            }

            if (receiver.unitType.Equals(BattleUnitType.MONSTER))
            {
                return !((BattleUnit_Monster)receiver).IsDamageImmune();
            }

            if (receiver.unitType.Equals(BattleUnitType.ITEM))
            {
                return !((BattleUnit_Item)receiver).IsDead();
            }

            return true;
        }

        private void SetTarget(BattleUnit unit)
        {
            if (this.targetUnit == null)
            {
                this.targetUnit = unit;
            }
            else if (this.targetUnit != unit)
            {
                this.targetUnit = unit;
            }
            else if (this.targetUnit == unit)
            {
                this.targetUnit = null;
            }
            this.SetCallBackList(unit);
        }

        private void SetCallBackList(BattleUnit unit)
        {
            if (unit == null) return;
            if (!this.curShouldCallBackDic.ContainsKey(unit))
            {
                this.curShouldCallBackDic[unit] = new List<BattleUnit>();
            }
            this.curShouldCallBackDic[unit].Clear();
        }

        public override void ClearInfo()
        {
            this.curShouldCallBackDic.Clear();
            this.registerHitTestShapeDic.Clear();
            this.registerUnitDic.Clear();
            this.targetUnit = null;
            this.spatialGrid = null;
            this.queryCandidates.Clear();
            this.querySeenUids.Clear();
            this.ResetAccumulators();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
