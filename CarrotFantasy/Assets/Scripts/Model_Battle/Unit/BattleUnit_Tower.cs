using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleUnit_Tower : BattleUnit
    {
        public Fix64 towerAttackRadius { get; private set; }
        public int towerID { get; private set; }

        private Fix64 attackCD; //攻击CD
        private Fix64 timeVal;  //攻击时间计时

        public bool isCanUpdate { get; private set; }
        public bool isMaxLevel { get; private set; }

        public int curLevel { get; private set; }
        public int[] price { get; private set; }

        public int curPrice;

        public int x { get; private set; }
        public int y { get; private set; } //地图坐标

        private UnitBeHitComponent unitBeHit;
        private UnitTransformComponent unitTrans;

        private List<BattleUnit_Monster> monsterList;
        public BattleUnit targetUnit { get; set; }

        public BattleUnit_Tower(BaseBattle battle) : base(battle)
        {
            this.unitType = BattleUnitType.TOWER;
            this.monsterList = new List<BattleUnit_Monster>();
        }

        public override void LoadInfo(int uid, Dictionary<string, Fix64> param, Fix64Vector2 birthPosition)
        {
            base.LoadInfo(uid, param, birthPosition);
            this.towerID = (int)param["towerID"];
            this.price = new int[3];
            this.price[0] = (int)param["price0"];
            this.price[1] = (int)param["price1"];
            this.price[2] = (int)param["price2"];
            this.attackCD = param["attackCD"];
            this.isCanUpdate = true;
            this.isMaxLevel = false;
            this.curLevel = 0;
            this.towerAttackRadius = param["bodyRadius0"];
            this.timeVal = Fix64.Zero;
        }

        public void LoadInfo1(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override void Init()
        {
            base.Init();
            this.unitBeHit = GameObjectPool.Instance.getNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (this.unitBeHit == null)
            {
                this.unitBeHit = new UnitBeHitComponent();
            }
            this.unitTrans = GameObjectPool.Instance.getNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.unitTrans == null)
            {
                this.unitTrans = new UnitTransformComponent();
            }
            this.AddComponent(this.unitBeHit);
            this.AddComponent(this.unitTrans);

            this.unitBeHit.RegisterBeHitCallBack(this.BeHitCallBack);
        }

        public override void InitComponents()
        {
            base.InitComponents();
            this.unitTrans.SetBodyRadius(this.towerAttackRadius);
        }

        private void BeHitCallBack(BattleUnit unit)
        {
            if (unit.unitType.Equals(BattleUnitType.MONSTER) == false)
            {
                Debug.Log(String.Format("防御塔碰撞过程出错，被碰撞对象为{0}", unit.unitType));
                return;
            }
            this.monsterList.Add((BattleUnit_Monster)unit);
        }

        public void UpdateLevel()
        {
            this.curLevel = this.curLevel + 1;
            this.curPrice = this.price[this.curLevel];
            this.isMaxLevel = this.curLevel == this.price.Length - 1 ? true : false;
            this.towerAttackRadius = this.birthParam["bodyRadius" + this.curLevel.ToString()];

            this.unitTrans.SetBodyRadius(this.towerAttackRadius);

            this.eventDipatcher.DispatchEvent<BattleUnit_Tower>(BattleEvent.TOWER_LEVEL_UP, this);
        }

        public override void OnTick(Fix64 deltaTime)
        {
            this.timeVal += deltaTime;
            if (this.timeVal >= this.attackCD)
            {
                BattleUnit targetOne = null;
                if (this.targetUnit != null)
                {
                    targetOne = this.targetUnit;
                }
                else
                {
                    if (this.monsterList.Count != 0)
                    {
                        BattleUnit_Monster curMonster = this.monsterList[0];
                        for (int i = 0; i <= monsterList.Count - 1; i++)
                        {
                            if (curMonster.EndPointDistance >= this.monsterList[i].EndPointDistance)
                            {
                                curMonster = this.monsterList[i];
                            }
                        }
                        targetOne = curMonster;
                    }
                }
                if (targetOne != null)
                {
                    this.eventDipatcher.DispatchEvent<BattleUnit>(BattleEvent.TOWER_ATTACK, targetOne);
                    this.baseBattle.eventDispatcher.DispatchEvent<BattleUnit_Tower, BattleUnit>(BattleEvent.BULLET_BUILD, this, targetOne);
                    this.timeVal = Fix64.Zero;
                }
            }
            this.monsterList.Clear();
            this.targetUnit = null;
        }

        public override void ClearInfo()
        {
            this.targetUnit = null;
            this.monsterList.Clear();
            base.ClearInfo();
        }

        public override void Dispose()
        {
            this.ClearInfo();
            base.Dispose();
        }
    }
}
