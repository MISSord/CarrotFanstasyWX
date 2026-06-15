using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 统一 PVE 开战入口（流程第 0 步）。
    /// 调用链：组装参数 → ApplyPveParams → LoadScene(BattleScene) → BeginSession → BaseBattle.LaunchParams。
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
                RoguelikeRunServer.Instance.CollectBattleModifiers(
                    out int startCoinBonus,
                    out int towerDamagePercentBonus);
                p.StartCoinBonus = startCoinBonus;
                p.TowerDamagePercentBonus = towerDamagePercentBonus;
            }

            StartPve(p);
        }

        /// <summary>
        /// 所有 PVE 模式的最终入口。参数经 ApplyPveParams 落盘，Session 创建时注入 BaseBattle.LaunchParams。
        /// </summary>
        private static void StartPve(PveModelBattleParams launchParams)
        {
            if (launchParams == null)
            {
                return;
            }

            MapServer.Instance?.RememberLastBattleLevel(launchParams.BigLevelId, launchParams.LevelId);

            BattleParamServer.Instance.ApplyPveParams(launchParams);
            BattleParamServer.Instance.EnsureBattleViewsLoaded();

            if (ServerProvision.sceneServer.IsLoading)
            {
                UIServer.Instance?.ShowTip("场景加载中，请稍候");
                return;
            }

            // 异步切 Unity 场景 → SceneServer 创建 BattleScene → BeginSession（见 BattleSessionHost）
            ServerProvision.sceneServer.LoadScene(
                BaseSceneType.BattleScene,
                null,
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
