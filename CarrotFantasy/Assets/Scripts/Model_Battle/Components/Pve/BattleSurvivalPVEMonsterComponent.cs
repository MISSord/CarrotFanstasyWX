using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 生存模式 PVE 专用怪物组件：按波次刷怪，怪物走流场移动（<see cref="BattleUnit_MonsterFlow"/>）。
    /// 与经典 <see cref="BattlePVEMonsterComponent"/> 分离，便于独立演进。
    /// </summary>
    public class BattleSurvivalPVEMonsterComponent : BattleMonsterComponent, IBattlePVEWaveMonster
    {
        private LevelInfo levelInfo;
        private List<Round.RoundInfo> roundInfo;
        private BattlePVEDataComponent battleDataComponent;
        private Fix64Vector2 birthPoint;
        private BattleFlowFieldComponent flowFieldComponent;

        public BattleSurvivalPVEMonsterComponent(BaseBattle bBattle) : base(bBattle)
        {
        }

        public static BattleSurvivalPVEMonsterComponent GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.MonsterComponent) as BattleSurvivalPVEMonsterComponent;
        }

        public override void Init()
        {
            PveModelBattleParams launchParams = BattleParamAccess.Current;
            if (launchParams != null)
            {
                this.levelInfo = launchParams.LevelInfo;
            }

            this.roundInfo = this.levelInfo != null ? this.levelInfo.roundInfo : null;
            this.battleDataComponent = BattlePVEDataComponent.GetFrom(this.baseBattle);

            BattlePVEMapComponent map = BattlePVEMapComponent.GetFrom(this.baseBattle);
            this.birthPoint = map != null ? map.startPoint : Fix64Vector2.Zero;

            BaseBattleComponent flowComp;
            if (this.baseBattle.componentDic.TryGetValue(BattleComponentType.FlowFieldComponent, out flowComp))
            {
                this.flowFieldComponent = flowComp as BattleFlowFieldComponent;
            }
            else
            {
                this.flowFieldComponent = null;
            }
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
                Debug.LogError("生存模式 PVE 怪物组件缺少数据或波次配置");
                return;
            }

            if (this.flowFieldComponent == null || !this.flowFieldComponent.IsBuilt)
            {
                Debug.LogError("生存模式 PVE 刷怪前流场未构建");
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
                this.curNoRegisterList.Add(this.CreateMonsterFlow(plan.MonsterIds[i]));
            }

            this.SetWaveSpawnSchedule(plan.SpawnOffsets);
            this.BeginWaveSpawn();
        }

        private BattleUnit_MonsterFlow CreateMonsterFlow(int monsterConfigId)
        {
            BattleUnit_MonsterFlow monster = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_MonsterFlow>(BattleUnitType.MONSTER_FLOW);
            if (monster == null)
            {
                monster = new BattleUnit_MonsterFlow(this.baseBattle);
            }

            monster.eventDipatcher.AddListener<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this.AddDeadList);
            monster.LoadInfo(
                this.baseBattle.GetUid(),
                this.monsterConfigReader.GetSingleMonsterConfig(monsterConfigId),
                this.birthPoint);
            monster.LoadInfo2(this.battleDataComponent.bigLevel, monsterConfigId);
            monster.Init();
            monster.LoadFlowMovement(this.flowFieldComponent);
            monster.InitComponents();
            return monster;
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
