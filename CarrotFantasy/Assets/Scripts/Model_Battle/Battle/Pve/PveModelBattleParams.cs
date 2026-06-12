using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 进入 PVE 战斗时由业务层传入的运行时参数（大小关、全局 Buff、模式、场景表现等）。
    /// </summary>
    public sealed class PveModelBattleParams
    {
        public int BigLevelId;
        public int LevelId;
        public LevelInfo LevelInfo;
        public Stage Stage;
        public SingleMapInfo SingleMapInfo;

        public BattlePveMode Mode = BattlePveMode.Classic;
        public int BattleRandomSeed;

        /// <summary>开场倒计时与首波刷怪前的统一等待时长（秒）。</summary>
        public const float DefaultStartGameDelaySeconds = 4f;

        public float StartGameDelaySeconds = DefaultStartGameDelaySeconds;

        public float GetEffectiveStartGameDelaySeconds()
        {
            return this.StartGameDelaySeconds > 0f
                ? this.StartGameDelaySeconds
                : DefaultStartGameDelaySeconds;
        }

        /// <summary>战斗视图挂载根节点；为空时使用当前场景 BattleRoot。</summary>
        public GameObject BattleViewRoot;

        /// <summary>本局全局 Buff Id 列表，由 <see cref="BattleGlobalBuffComponent"/> 解析并应用。</summary>
        public List<int> GlobalBuffIds = new List<int>();

        /// <summary>进战斗额外起始金币（与 GlobalBuffIds 解析结果叠加）。</summary>
        public int StartCoinBonus;

        /// <summary>塔伤害百分比加成（与 GlobalBuffIds 解析结果叠加）。</summary>
        public int TowerDamagePercentBonus;

        public static PveModelBattleParams CreateClassic(int bigLevelId, int levelId)
        {
            var p = new PveModelBattleParams
            {
                BigLevelId = bigLevelId,
                LevelId = levelId,
                Mode = BattlePveMode.Classic,
            };
            p.EnsureLevelDataLoaded();
            return p;
        }

        public static PveModelBattleParams CreateRoguelike(int bigLevelId, int levelId)
        {
            PveModelBattleParams p = CreateClassic(bigLevelId, levelId);
            p.Mode = BattlePveMode.Roguelike;
            return p;
        }

        /// <summary>从场景切换字典解析可选参数。键：<c>battleViewRoot</c>、<c>startGameDelay</c>、<c>battleRandomSeed</c>。</summary>
        public static PveModelBattleParams FromDictionary(Dictionary<string, object> param)
        {
            PveModelBattleParams p = new PveModelBattleParams();
            if (param == null)
            {
                return p;
            }

            if (param.TryGetValue("battleViewRoot", out object rootObj) && rootObj is GameObject go)
            {
                p.BattleViewRoot = go;
            }

            if (param.TryGetValue("startGameDelay", out object delayObj) && delayObj != null &&
                TryToSingle(delayObj, out float delay))
            {
                p.StartGameDelaySeconds = delay;
            }

            if (param.TryGetValue("battleRandomSeed", out object seedObj) && seedObj != null &&
                int.TryParse(seedObj.ToString(), out int seed))
            {
                p.BattleRandomSeed = seed;
            }

            return p;
        }

        public void EnsureLevelDataLoaded()
        {
            if (this.LevelInfo == null && this.BigLevelId > 0 && this.LevelId > 0 &&
                BattleParamServer.Instance != null)
            {
                string path = "Level" + this.BigLevelId + "_" + this.LevelId + ".json";
                this.LevelInfo = BattleParamServer.Instance.LoadLevelInfoFile(path);
            }

            if (MapServer.Instance == null || MapServer.Instance.mapModel == null)
            {
                return;
            }

            if (this.Stage == null && this.BigLevelId > 0 && this.LevelId > 0)
            {
                this.Stage = MapServer.Instance.mapModel.GetStage(this.BigLevelId, this.LevelId);
            }

            if (this.SingleMapInfo == null && this.BigLevelId > 0 && this.LevelId > 0)
            {
                this.SingleMapInfo = MapServer.Instance.mapModel.GetSingleMapInfo(this.BigLevelId, this.LevelId);
            }
        }

        private static bool TryToSingle(object value, out float result)
        {
            switch (value)
            {
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    try
                    {
                        result = Convert.ToSingle(value);
                        return true;
                    }
                    catch
                    {
                        result = 0f;
                        return false;
                    }
            }
        }
    }
}
