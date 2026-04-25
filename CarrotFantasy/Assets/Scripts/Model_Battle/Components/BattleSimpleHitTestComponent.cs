using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleSimpleHitTestComponent : BaseBattleComponent
    {
        private Dictionary<string, List<BattleUnit>> registerUnitDic = new Dictionary<string, List<BattleUnit>>();
        private Dictionary<string, List<UnitTransformComponent>> registerHitTestShapeDic = new Dictionary<string, List<UnitTransformComponent>>();

        private Dictionary<BattleUnit, List<BattleUnit>> curShouldCallBackDic = new Dictionary<BattleUnit, List<BattleUnit>>();

        private BattleUnit targetUnit = null;

        public BattleSimpleHitTestComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.HitTestComponent;
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
            this.ChooseSingleBeHit(BattleUnitType.MONSTER, BattleUnitType.BULLET);
            this.ChooseSingleBeHit(BattleUnitType.MONSTER, BattleUnitType.TOWER);
            this.ChooseSingleBeHit(BattleUnitType.ITEM, BattleUnitType.BULLET);

            if (this.targetUnit != null)
            {
                this.ChooseSingleBeHit();
            }

            this.ExeTheCallBack();
        }

        private void ChooseSingleBeHit(String type1, String type2)
        {
            UnitTransformComponent unit1;
            UnitTransformComponent unit2;
            for (int i = 0; i <= this.registerHitTestShapeDic[type1].Count - 1; i++)
            {
                unit1 = this.registerHitTestShapeDic[type1][i];
                for (int j = 0; j <= this.registerHitTestShapeDic[type2].Count - 1; j++)
                {
                    unit2 = this.registerHitTestShapeDic[type2][j];
                    bool isHit = HitTestHandler.HitTest(unit1.bodyHitTestShape, unit2.bodyHitTestShape);
                    if (isHit == true)
                    {
                        this.curShouldCallBackDic[unit1.unit].Add(unit2.unit);
                    }
                }
            }
        }

        private void ChooseSingleBeHit()
        {
            UnitTransformComponent unit1;
            UnitTransformComponent unit2 = (UnitTransformComponent)this.targetUnit.GetComponent(UnitComponentType.TRANSFORM);
            for (int i = 0; i <= this.registerHitTestShapeDic[BattleUnitType.TOWER].Count - 1; i++)
            {
                unit1 = this.registerHitTestShapeDic[BattleUnitType.TOWER][i];
                bool isHit = HitTestHandler.HitTest(unit1.bodyHitTestShape, unit2.bodyHitTestShape);
                if (isHit == true)
                {
                    ((BattleUnit_Tower)this.registerUnitDic[BattleUnitType.TOWER][i]).targetUnit = this.targetUnit;
                }
            }
        }

        private void ExeTheCallBack()
        {
            if (this.curShouldCallBackDic.Count == 0) return;
            foreach (KeyValuePair<BattleUnit, List<BattleUnit>> info in this.curShouldCallBackDic)
            {
                UnitBeHitComponent tranBeHit = (UnitBeHitComponent)info.Key.GetComponent(UnitComponentType.BEHIT);
                for (int i = 0; i <= info.Value.Count - 1; i++)
                {
                    UnitBeHitComponent beHit = (UnitBeHitComponent)info.Value[i].GetComponent(UnitComponentType.BEHIT);
                    beHit.BeHitCallBack(info.Key);
                    tranBeHit.BeHitCallBack(info.Value[i]);
                }
                info.Value.Clear();
            }
        }

        private void SetTarget(BattleUnit unit)
        {
            if (this.targetUnit == null)
            {
                this.targetUnit = unit;
            }
            //转换新目标
            else if (this.targetUnit != unit)
            {
                this.targetUnit = unit;
            }
            //两次点击的是同一个目标
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
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
