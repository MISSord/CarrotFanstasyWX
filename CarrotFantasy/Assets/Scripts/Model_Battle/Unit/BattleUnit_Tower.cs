using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 单塔逻辑：射程扫描、CD、选目标、派发 BULLET_BUILD。
    /// 集火 targetUnit 由 HitTest.AssignTowerFocusTargets 写入；无集火时只自动打怪物。
    /// </summary>
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
        /// <summary>HitTest 写入的玩家集火目标；在射程内时优先于自动选怪。</summary>
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
            this.unitBeHit = BattleUnitPool.Instance.GetNewUnitComponent<UnitBeHitComponent>(UnitComponentType.BEHIT);
            if (this.unitBeHit == null)
            {
                this.unitBeHit = new UnitBeHitComponent();
            }
            this.unitTrans = BattleUnitPool.Instance.GetNewUnitComponent<UnitTransformComponent>(UnitComponentType.TRANSFORM);
            if (this.unitTrans == null)
            {
                this.unitTrans = new UnitTransformComponent();
            }
            this.AddComponent(this.unitBeHit);
            this.AddComponent(this.unitTrans);
        }

        public override void InitComponents()
        {
            base.InitComponents();
            this.unitTrans.SetBodyRadius(this.towerAttackRadius);
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
            this.monsterList.Clear();
            this.CollectMonstersInRange();

            this.timeVal += deltaTime;
            if (this.timeVal >= this.attackCD)
            {
                BattleUnit targetOne = null;
                if (this.targetUnit != null && this.IsAttackTargetInRange(this.targetUnit))
                {
                    targetOne = this.targetUnit;
                }
                else if (this.monsterList.Count != 0)
                {
                    BattleUnit_Monster curMonster = this.monsterList[0];
                    for (int i = 0; i <= this.monsterList.Count - 1; i++)
                    {
                        if (curMonster.EndPointDistance >= this.monsterList[i].EndPointDistance)
                        {
                            curMonster = this.monsterList[i];
                        }
                    }

                    targetOne = curMonster;
                }

                if (targetOne != null)
                {
                    this.eventDipatcher.DispatchEvent<BattleUnit>(BattleEvent.TOWER_ATTACK, targetOne);
                    this.baseBattle.eventDispatcher.DispatchEvent<BattleUnit_Tower, BattleUnit>(
                        BattleEvent.BULLET_BUILD,
                        this,
                        targetOne);
                    this.timeVal = Fix64.Zero;
                }
                else
                {
                    // 无目标时不累积超额 CD，避免首次入射程或久未攻击后连发。
                    this.timeVal = this.attackCD;
                }
            }
        }

        /// <summary>主动扫描攻击范围内的怪物，不依赖 HitTest 回调顺序。</summary>
        void CollectMonstersInRange()
        {
            if (this.unitTrans == null || this.baseBattle == null)
            {
                return;
            }

            BattleMonsterComponent monsterComponent =
                (BattleMonsterComponent)this.baseBattle.GetComponent(BattleComponentType.MonsterComponent);
            if (monsterComponent == null || monsterComponent.curMonsterDic == null)
            {
                return;
            }

            foreach (KeyValuePair<int, BattleUnit_Monster> pair in monsterComponent.curMonsterDic)
            {
                BattleUnit_Monster monster = pair.Value;
                if (monster == null || monster.IsDamageImmune())
                {
                    continue;
                }

                UnitTransformComponent monsterTrans =
                    (UnitTransformComponent)monster.GetComponent(UnitComponentType.TRANSFORM);
                if (monsterTrans == null)
                {
                    continue;
                }

                if (BattleRangeQuery.IsInRange(this.unitTrans, monsterTrans))
                {
                    this.monsterList.Add(monster);
                }
            }
        }

        bool IsAttackTargetInRange(BattleUnit unit)
        {
            if (unit == null || this.unitTrans == null)
            {
                return false;
            }

            if (unit.unitType.Equals(BattleUnitType.MONSTER))
            {
                BattleUnit_Monster monster = (BattleUnit_Monster)unit;
                if (monster.IsDamageImmune())
                {
                    return false;
                }
            }
            else if (unit.unitType.Equals(BattleUnitType.ITEM))
            {
                BattleUnit_Item item = (BattleUnit_Item)unit;
                if (item.IsDead())
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            UnitTransformComponent targetTrans = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            if (targetTrans == null)
            {
                return false;
            }

            return BattleRangeQuery.IsInRange(this.unitTrans, targetTrans);
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
