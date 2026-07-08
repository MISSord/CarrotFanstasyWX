using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 怪物组件基类：在场怪物字典、死亡回收、Tick 与调度注册。
    /// </summary>
    public class BattleMonsterComponent : BaseBattleComponent
    {
        public Dictionary<int, BattleUnit_Monster> curMonsterDic { get; protected set; }

        protected List<BattleUnit_Monster> curNoRegisterList;

        protected List<BattleUnit_Monster> curDeadMonsterList;

        protected MonsterConfigReader monsterConfigReader;

        public int scheId { get; set; }

        private List<float> waveSpawnOffsets;
        private int nextSpawnIndex;
        private Fix64 waveSpawnStartClock;
        private bool waveSpawnActive;

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

        /// <summary>设置本波每只怪相对波次开始的时间偏移（秒），与 <see cref="curNoRegisterList"/> 顺序一致。</summary>
        public void SetWaveSpawnSchedule(IList<float> offsets)
        {
            this.waveSpawnOffsets = offsets != null ? new List<float>(offsets) : new List<float>();
            this.nextSpawnIndex = 0;
            this.waveSpawnActive = this.waveSpawnOffsets.Count > 0;
        }

        /// <summary>记录本波起点战斗时钟，配合 <see cref="SetWaveSpawnSchedule"/> 在 Tick 中按时间轴出场。</summary>
        public void BeginWaveSpawn()
        {
            this.waveSpawnStartClock = this.baseBattle.curClock;
            this.nextSpawnIndex = 0;
            this.waveSpawnActive = this.waveSpawnOffsets != null && this.waveSpawnOffsets.Count > 0;
        }

        public void RegisterNewMonster()
        {
            if (this.curNoRegisterList.Count == 0)
            {
                return;
            }

            BattleUnit_Monster monster = this.curNoRegisterList[0];
            this.curNoRegisterList.RemoveAt(0);
            if (this.curMonsterDic.ContainsKey(monster.uid))
            {
                Debug.LogError(
                    "[BattleMonsterComponent] 重复注册怪物 uid=" + monster.uid +
                    "，请检查离关是否未 ClearInfo 或对象池复用未重置。");
                return;
            }

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
            this.StopWaveSpawnSchedule();
            Debug.Log("注册新的怪兽工作完成");
        }

        protected void StopWaveSpawnSchedule()
        {
            this.waveSpawnActive = false;
            this.waveSpawnOffsets = null;
            this.nextSpawnIndex = 0;
        }

        private void TickWaveSpawnSchedule()
        {
            if (!this.waveSpawnActive || this.waveSpawnOffsets == null || this.curNoRegisterList.Count == 0)
            {
                return;
            }

            Fix64 now = this.baseBattle.curClock;
            while (this.waveSpawnActive
                   && this.waveSpawnOffsets != null
                   && this.nextSpawnIndex < this.waveSpawnOffsets.Count
                   && this.curNoRegisterList.Count > 0)
            {
                Fix64 releaseAt = this.waveSpawnStartClock + new Fix64(this.waveSpawnOffsets[this.nextSpawnIndex]);
                if (now < releaseAt)
                {
                    break;
                }

                this.RegisterNewMonster();
                this.nextSpawnIndex++;
            }
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
            this.TickWaveSpawnSchedule();
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

            BattleSchedulerComponent sche = this.baseBattle.GetComponent(BattleComponentType.SchedulerComponent) as BattleSchedulerComponent;
            if (sche == null)
            {
                this.scheId = 0;
                return;
            }

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
            this.StopWaveSpawnSchedule();
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
            this.ClearInfo();
            base.Dispose();
        }
    }
}
