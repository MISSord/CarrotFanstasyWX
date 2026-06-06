using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 怪物组件基类：在场怪物字典、死亡回收、Tick 与调度注册。
    /// PVE 波次刷怪见 <see cref="BattlePVEMonsterComponent"/>、<see cref="BattleSurvivalPVEMonsterComponent"/>。
    /// </summary>
    public class BattleMonsterComponent : BaseBattleComponent
    {
        public Dictionary<int, BattleUnit_Monster> curMonsterDic { get; protected set; }

        protected List<BattleUnit_Monster> curNoRegisterList;

        protected List<BattleUnit_Monster> curDeadMonsterList;

        protected MonsterConfigReader monsterConfigReader;

        public int scheId { get; set; }

        public override void Init()
        {
        }

        public BattleMonsterComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.MonsterComponent;
            this.curMonsterDic = new Dictionary<int, BattleUnit_Monster>();
            this.curNoRegisterList = new List<BattleUnit_Monster>();
            this.curDeadMonsterList = new List<BattleUnit_Monster>();
            this.scheId = 0;
            this.monsterConfigReader = MonsterConfigReader.Instance;
        }

        public void RegisterNewMonster()
        {
            if (this.curNoRegisterList.Count == 0)
            {
                return;
            }

            BattleUnit_Monster monster = this.curNoRegisterList[0];
            this.curNoRegisterList.RemoveAt(0);
            this.curMonsterDic.Add(monster.uid, monster);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.MONSTER, monster);
            if (this.curNoRegisterList.Count == 0)
            {
                this.OnAllPendingMonstersRegistered();
            }
        }

        protected virtual void OnAllPendingMonstersRegistered()
        {
            this.RemoveSchId();
            Debug.Log("注册新的怪兽工作完成");
        }

        protected void AddDeadList(BattleUnit_Monster monster)
        {
            this.curDeadMonsterList.Add(monster);
        }

        public void CheckSingleMonsterState(BattleUnit_Monster monster)
        {
            if (monster.IsDead())
            {
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.MONSTER, monster);
                this.baseBattle.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, 50);
                monster.ClearInfo();
                this.curMonsterDic.Remove(monster.uid);
                BattleUnitPool.Instance.PushObjectToPool(BattleUnit_Monster.GetMonsterPoolKey(monster), monster);
            }
        }

        public override void OnTick(Fix64 time)
        {
            base.OnTick(time);
            foreach (KeyValuePair<int, BattleUnit_Monster> info in this.curMonsterDic)
            {
                info.Value.OnTick(time);
            }

            this.UpdateCurMonsterWavesState();
        }

        public override void LateTick(Fix64 time)
        {
            base.LateTick(time);
            this.UpdateCurMonsterWaveStateLateTick(time);
        }

        public void UpdateCurMonsterWavesState()
        {
            if (this.curDeadMonsterList.Count == 0)
            {
                return;
            }

            for (int i = 0; i < this.curDeadMonsterList.Count; i++)
            {
                this.CheckSingleMonsterState(this.curDeadMonsterList[i]);
            }

            this.curDeadMonsterList.Clear();
        }

        public void UpdateCurMonsterWaveStateLateTick(Fix64 time)
        {
            foreach (KeyValuePair<int, BattleUnit_Monster> info in this.curMonsterDic)
            {
                info.Value.LateTick(time);
            }
        }

        public void RemoveSchId()
        {
            if (this.scheId == 0)
            {
                return;
            }

            BattleSchedulerComponent sche = (BattleSchedulerComponent)this.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
            sche.SilenceSingleSche(this.scheId);
            this.scheId = 0;
        }

        public bool CheckIsHaveAnyMonsterSurvive()
        {
            return this.curMonsterDic.Count != 0 || this.curNoRegisterList.Count != 0;
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            foreach (KeyValuePair<int, BattleUnit_Monster> info in this.curMonsterDic)
            {
                info.Value.ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnit_Monster.GetMonsterPoolKey(info.Value), info.Value);
            }

            for (int i = 0; i < this.curNoRegisterList.Count; i++)
            {
                this.curNoRegisterList[i].ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnit_Monster.GetMonsterPoolKey(this.curNoRegisterList[i]), this.curNoRegisterList[i]);
            }

            this.curNoRegisterList.Clear();
            this.curMonsterDic.Clear();
            this.curDeadMonsterList.Clear();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
