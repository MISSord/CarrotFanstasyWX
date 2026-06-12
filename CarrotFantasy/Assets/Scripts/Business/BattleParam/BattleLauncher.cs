using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>统一 PVE 开战入口：传入大小关与全局 Buff 后切战斗场景。</summary>
    public static class BattleLauncher
    {
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

        public static void StartPve(PveModelBattleParams launchParams)
        {
            if (launchParams == null)
            {
                return;
            }

            launchParams.EnsureLevelDataLoaded();
            MapServer.Instance?.RememberLastBattleLevel(launchParams.BigLevelId, launchParams.LevelId);
            BattleParamServer.Instance.ApplyPveParams(launchParams);
            BattleParamServer.Instance.EnsureBattleViewsLoaded();

            if (ServerProvision.sceneServer.IsLoading)
            {
                UIServer.Instance?.ShowTip("场景加载中，请稍候");
                return;
            }

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
