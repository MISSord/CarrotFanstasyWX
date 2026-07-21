using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 统一 PVE 开战入口（流程第 0 步）。
    /// 调用链：组装参数 → LoadScene(BattleScene, params) → BeginSession → BaseBattle.LaunchParams。
    /// </summary>
    public static class BattleLauncher
    {
        /// <summary>经典模式进关；可选全局 Buff 写入开战参数。</summary>
        public static void StartClassicLevel(int bigLevelId, int levelId, IList<int> globalBuffIds = null)
        {
            PveModelBattleParams p = PveModelBattleParams.CreateClassic(bigLevelId, levelId);
            AppendGlobalBuffIds(p, globalBuffIds);
            StartPve(p);
        }

        public static void StartRoguelikeEncounter(int encounterId, int bigLevelId, int levelId)
        {
            PveModelBattleParams p = PveModelBattleParams.CreateRoguelike(bigLevelId, levelId);
            if (RoguelikeRunServer.Instance != null && RoguelikeRunServer.Instance.IsRunActive)
            {
                var mods = new RoguelikeBattleModifiers();
                RoguelikeRunServer.Instance.CollectBattleModifiers(mods);
                p.StartCoinBonus = mods.StartCoinBonus;
                p.TowerDamagePercentBonus = mods.TowerDamagePercentBonus;
                AppendGlobalBuffIds(p, mods.GlobalBuffIds);
            }

            StartPve(p);
        }

        /// <summary>
        /// 所有 PVE 模式的最终入口。参数随场景传递，Session 创建时注入 BaseBattle.LaunchParams。
        /// </summary>
        private static void StartPve(PveModelBattleParams launchParams)
        {
            if (launchParams == null)
            {
                return;
            }

            launchParams.EnsureLevelDataLoaded();
            MapServer.Instance?.RememberLastBattleLevel(launchParams.BigLevelId, launchParams.LevelId);

            BattleParamServer.Instance.EnsureBattleViewsLoaded();

            if (ServerProvision.sceneServer.IsLoading)
            {
                UIServer.Instance?.ShowTip("场景加载中，请稍候");
                return;
            }

            var sceneParam = new Dictionary<string, dynamic>
            {
                { BattleSceneParamKeys.PveLaunchParams, launchParams },
            };

            ServerProvision.sceneServer.LoadScene(
                BaseSceneType.BattleScene,
                sceneParam,
                success =>
                {
                    if (!success)
                    {
                        UIServer.Instance?.ShowTip("进入战斗失败，请重试");
                    }
                });
        }

        private static void AppendGlobalBuffIds(PveModelBattleParams p, IList<int> globalBuffIds)
        {
            if (p == null || globalBuffIds == null || globalBuffIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < globalBuffIds.Count; i++)
            {
                p.GlobalBuffIds.Add(globalBuffIds[i]);
            }
        }
    }
}
