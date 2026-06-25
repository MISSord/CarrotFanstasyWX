using System;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CarrotFantasy
{
    /// <summary>
    /// 原版全量二重循环碰撞（无空间网格），用于与 <see cref="BattleSimpleHitTestComponent"/> 做性能对比。
    /// </summary>
    public class BattleBruteForceHitTestComponent : BaseBattleComponent, IHitTestPerfStats
    {
        private Dictionary<string, List<BattleUnit>> registerUnitDic = new Dictionary<string, List<BattleUnit>>();
        private Dictionary<string, List<UnitTransformComponent>> registerHitTestShapeDic = new Dictionary<string, List<UnitTransformComponent>>();

        private Dictionary<BattleUnit, List<BattleUnit>> curShouldCallBackDic = new Dictionary<BattleUnit, List<BattleUnit>>();

        private BattleUnit targetUnit = null;

        private readonly Stopwatch tickStopwatch = new Stopwatch();

        public string ModeName
        {
            get { return "BruteForce"; }
        }

        public long LastTickElapsedTicks { get; private set; }

        public int LastNarrowPhaseCount { get; private set; }

        public int LastBroadPhasePairCount { get; private set; }

        public long AccumulatedElapsedTicks { get; private set; }

        public int SampleFrameCount { get; private set; }

        public BattleBruteForceHitTestComponent(BaseBattle bBattle) : base(bBattle)
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

            this.ChooseSingleBeHit(BattleUnitType.MONSTER, BattleUnitType.BULLET);
            this.ChooseSingleBeHit(BattleUnitType.ITEM, BattleUnitType.BULLET);

            if (this.targetUnit != null)
            {
                this.ChooseSingleBeHitForTarget();
            }

            this.ExeTheCallBack();

            this.tickStopwatch.Stop();
            this.LastTickElapsedTicks = this.tickStopwatch.ElapsedTicks;
            this.AccumulatedElapsedTicks += this.LastTickElapsedTicks;
            this.SampleFrameCount += 1;
        }

        private void ChooseSingleBeHit(String type1, String type2)
        {
            List<UnitTransformComponent> list1 = this.registerHitTestShapeDic[type1];
            List<UnitTransformComponent> list2 = this.registerHitTestShapeDic[type2];
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

                for (int j = 0; j < list2.Count; j++)
                {
                    UnitTransformComponent unit2 = list2[j];
                    this.LastBroadPhasePairCount += 1;
                    this.LastNarrowPhaseCount += 1;
                    if (HitTestHandler.HitTest(unit1.bodyHitTestShape, unit2.bodyHitTestShape))
                    {
                        callbackList.Add(unit2.unit);
                    }
                }
            }
        }

        private void ChooseSingleBeHitForTarget()
        {
            UnitTransformComponent targetTransform = (UnitTransformComponent)this.targetUnit.GetComponent(UnitComponentType.TRANSFORM);
            if (targetTransform == null)
            {
                return;
            }

            List<UnitTransformComponent> towers = this.registerHitTestShapeDic[BattleUnitType.TOWER];
            for (int i = 0; i < towers.Count; i++)
            {
                UnitTransformComponent towerTransform = towers[i];
                this.LastBroadPhasePairCount += 1;
                this.LastNarrowPhaseCount += 1;
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
