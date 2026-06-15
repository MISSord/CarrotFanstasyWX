using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 经典 PVE 怪物组件：按关卡波次刷怪，沿折线路径移动（<see cref="BattleUnit_Monster_Pve"/>）。
    /// </summary>
    public class BattlePVEMonsterComponent : BattleMonsterComponent, IBattlePVEWaveMonster
    {
        private LevelInfo levelInfo;
        private List<Round.RoundInfo> roundInfo;
        private BattlePVEDataComponent battleDataComponent;
        private Fix64Vector2 birthPoint;
        private List<Fix64Vector2> monsterPointList;
        private Fix64 distance;

        public BattlePVEMonsterComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public static BattlePVEMonsterComponent GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.MonsterComponent) as BattlePVEMonsterComponent;
        }

        public override void Init()
        {
            PveModelBattleParams launchParams = this.baseBattle.LaunchParams;
            if (launchParams != null)
            {
                this.levelInfo = launchParams.LevelInfo;
            }

            this.roundInfo = this.levelInfo != null ? this.levelInfo.roundInfo : null;
            this.battleDataComponent = BattlePVEDataComponent.GetFrom(this.baseBattle);

            BattlePVEMapComponent map = BattlePVEMapComponent.GetFrom(this.baseBattle);
            this.birthPoint = map != null ? map.startPoint : Fix64Vector2.Zero;

            this.distance = Fix64.Zero;
            this.CalcaTheTotalDistance();
        }

        public void BuildNewWavesMonster()
        {
            if (this.curNoRegisterList.Count != 0)
            {
                Debug.LogError("当前怪物注册列表没有清空");
                return;
            }

            if (this.curMonsterDic.Count != 0)
            {
                Debug.LogError("当前怪物字典没有清空");
                return;
            }

            if (this.battleDataComponent == null || this.roundInfo == null)
            {
                Debug.LogError("PVE 怪物组件缺少数据或波次配置");
                return;
            }

            int curWaves = this.battleDataComponent.curWaves;
            if (curWaves <= 0 || curWaves > this.roundInfo.Count)
            {
                Debug.LogError(String.Format("当前波次非法: {0}", curWaves));
                return;
            }

            Round.RoundInfo curRound = this.roundInfo[curWaves - 1];
            WaveSpawnPlan plan = SpawnPlanCompiler.Compile(curRound);
            if (plan.Count == 0)
            {
                Debug.LogError(String.Format("当前波次无怪物配置: wave={0}", curWaves));
                return;
            }

            for (int i = 0; i < plan.Count; i++)
            {
                BattleUnit_Monster_Pve monster = this.CreateMonsterPve(plan.MonsterIds[i]);
                if (monster != null)
                {
                    this.curNoRegisterList.Add(monster);
                }
            }

            this.SetWaveSpawnSchedule(plan.SpawnOffsets);
            this.BeginWaveSpawn();
        }

        private BattleUnit_Monster_Pve CreateMonsterPve(int monsterConfigId)
        {
            Dictionary<string, Fix64> config = this.monsterConfigReader.GetSingleMonsterConfig(monsterConfigId);
            if (config == null)
            {
                return null;
            }

            BattleUnit_Monster_Pve monster = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Monster_Pve>(BattleUnitType.MONSTER);
            if (monster == null)
            {
                monster = new BattleUnit_Monster_Pve(this.baseBattle);
            }

            monster.LoadInfo(
                this.baseBattle.GetUid(),
                config,
                this.birthPoint);
            monster.LoadInfo2(this.battleDataComponent.bigLevel, monsterConfigId);
            monster.eventDipatcher.AddListener<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this.AddDeadList);
            monster.Init();
            monster.InitComponents();
            monster.LoadPathMovement(this.monsterPointList, this.distance);
            return monster;
        }

        private void CalcaTheTotalDistance()
        {
            BattlePVEMapComponent mapComponent = BattlePVEMapComponent.GetFrom(this.baseBattle);
            if (mapComponent == null || mapComponent.monsterPathList == null)
            {
                this.monsterPointList = new List<Fix64Vector2>();
                return;
            }

            this.monsterPointList = mapComponent.monsterPathList;
            for (int i = 0; i <= this.monsterPointList.Count - 1; i++)
            {
                if (i + 1 >= this.monsterPointList.Count)
                {
                    break;
                }

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

        public bool IsCanNewMonsterWaves()
        {
            if (this.battleDataComponent == null || this.roundInfo == null)
            {
                return false;
            }

            if (this.battleDataComponent.curWaves >= this.roundInfo.Count)
            {
                return false;
            }

            return true;
        }
    }
}
