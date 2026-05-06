using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BattleMonsterComponent : BaseBattleComponent
    {
        public Dictionary<int, BattleUnit_Monster> curMonsterDic { get; private set; }
        private List<BattleUnit_Monster> curNoRegisterList;

        private LevelInfo levelInfo;
        private List<Round.RoundInfo> roundInfo;

        private BattleDataComponent battleDataComponent;

        private MonsterConfigReader monsterConfigReader;

        private bool isHaveNoRegisterMonster = false;

        private List<BattleUnit_Monster> curDeadMonsterList;

        private Fix64Vector2 birthPoint;
        public int scheId { get; set; } //这个由状态机赋值

        private List<Fix64Vector2> monsterPointList; //怪兽路径

        private Fix64 distance;

        public BattleMonsterComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = BattleComponentType.MonsterComponent;

            this.curMonsterDic = new Dictionary<int, BattleUnit_Monster>();
            this.curNoRegisterList = new List<BattleUnit_Monster>();
            this.curDeadMonsterList = new List<BattleUnit_Monster>();
            this.scheId = 0;

            this.monsterConfigReader = new MonsterConfigReader();
            this.monsterConfigReader.Init();
        }

        public override void Init()
        {
            this.levelInfo = BattleParamServer.Instance.info;
            this.roundInfo = levelInfo.roundInfo;

            this.battleDataComponent = (BattleDataComponent)this.baseBattle.GetComponent(BattleComponentType.DataComponent);
            BattleMapComponent map = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
            this.birthPoint = map.startPoint;

            this.CalcaTheTotalDistance();
        }

        public void BuildNewWavesMonster()
        {
            if (this.curNoRegisterList.Count != 0)
            {
                Debug.LogError("当前怪物注册列表没有清空");
                return;
            }
            else if (this.curMonsterDic.Count != 0)
            {
                Debug.LogError("当前怪物字典没有清空");
                return;
            }
            int curWaves = this.battleDataComponent.curWaves;
            Round.RoundInfo curMonsterList = this.roundInfo[curWaves - 1];

            for (int i = 0; i < curMonsterList.mMonsterIDList.Length; i++)
            {
                BattleUnit_Monster monster = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Monster>(BattleUnitType.MONSTER);
                if (monster == null)
                {
                    monster = new BattleUnit_Monster(this.baseBattle);
                }
                monster.eventDipatcher.AddListener<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this.AddDeadList);
                monster.LoadInfo(this.baseBattle.GetUid(), this.monsterConfigReader.GetSingleMonsterConfig(this.GetMonsterId(curMonsterList.mMonsterIDList[i])), birthPoint);
                monster.LoadInfo2(this.battleDataComponent.bigLevel, curMonsterList.mMonsterIDList[i]);
                monster.Init();
                monster.LoadInfo3(this.monsterPointList, this.distance);
                monster.InitComponents();
                this.curNoRegisterList.Add(monster);
            }
            this.isHaveNoRegisterMonster = true;
        }

        private void CalcaTheTotalDistance()
        {
            BattleMapComponent mapComponent = (BattleMapComponent)this.baseBattle.GetComponent(BattleComponentType.MapComponent);
            this.monsterPointList = mapComponent.monsterPathList;
            for (int i = 0; i <= this.monsterPointList.Count - 1; i++)
            {
                if (i + 1 >= this.monsterPointList.Count) break;
                if (this.monsterPointList[i].X == this.monsterPointList[i + 1].X)
                {
                    if (this.monsterPointList[i].Y >= this.monsterPointList[i + 1].Y)
                    {
                        this.distance += this.monsterPointList[i].Y - this.monsterPointList[i + 1].Y;
                    }
                    else
                    {
                        this.distance += this.monsterPointList[i + 1].Y - this.monsterPointList[i].Y;
                    }
                }
                else
                {
                    if (this.monsterPointList[i].X >= this.monsterPointList[i + 1].X)
                    {
                        this.distance += this.monsterPointList[i].X - this.monsterPointList[i + 1].X;
                    }
                    else
                    {
                        this.distance += this.monsterPointList[i + 1].X - this.monsterPointList[i].X;
                    }
                }
            }
        }

        public int GetMonsterId(int monsterId)
        {
            return this.battleDataComponent.bigLevel * 100 + monsterId;
        }

        public void RegisterNewMonster()
        {
            BattleUnit_Monster monster = this.curNoRegisterList[0];
            this.curNoRegisterList.RemoveAt(0);
            this.curMonsterDic.Add(monster.uid, monster);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.MONSTER, monster);
            //Debug.Log(String.Format("注册新的怪兽,怪兽id{0}_{1}",monster.curLevel,monster.monsterId));
            if (this.curNoRegisterList.Count == 0)
            {
                this.isHaveNoRegisterMonster = false;
                this.RemoveSchId();
                Debug.Log("注册新的怪兽工作完成");
            }
        }

        private void AddDeadList(BattleUnit_Monster monster)
        {
            this.curDeadMonsterList.Add(monster);
        }

        public void CheckSingleMonsterState(BattleUnit_Monster monster)
        {
            if (monster.IsDead() == true)
            {
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.MONSTER, monster);
                this.baseBattle.eventDispatcher.DispatchEvent<int>(BattleEvent.COIN_CHANGE, 50);
                //先从其他组件上除去，再从视图移除，最后再自己移除，确保顺序
                monster.ClearInfo();
                this.curMonsterDic.Remove(monster.uid);
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.MONSTER, monster);
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
            if (this.curDeadMonsterList.Count != 0)
            {
                for (int i = 0; i < this.curDeadMonsterList.Count; i++)
                {
                    this.CheckSingleMonsterState(this.curDeadMonsterList[i]);
                }
                this.curDeadMonsterList.Clear();
            }
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
            if (this.scheId != 0)
            {
                BattleSchedulerComponent sche = (BattleSchedulerComponent)this.baseBattle.GetComponent(BattleComponentType.SchedulerComponent);
                sche.SilenceSingleSche(this.scheId);
                this.scheId = 0;
            }
        }

        public bool CheckIsHaveAnyMonsterSurvive()
        {
            if (this.curMonsterDic.Count != 0)
            {
                return true;
            }
            if (this.curNoRegisterList.Count != 0)
            {
                return true;
            }
            return false;
        }

        public bool IsCanNewMonsterWaves()
        {
            if (this.battleDataComponent.curWaves >= this.roundInfo.Count)
            {
                return false;
            }
            return true;
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            foreach (KeyValuePair<int, BattleUnit_Monster> info in this.curMonsterDic)
            {
                info.Value.ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.MONSTER, info.Value);
            }
            for (int i = 0; i <= this.curNoRegisterList.Count - 1; i++)
            {
                this.curNoRegisterList[i].ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.MONSTER, this.curNoRegisterList[i]);
            }
            this.curNoRegisterList.Clear();
            this.curMonsterDic.Clear();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

    }
}
