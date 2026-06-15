using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图资源预加载（Session 流程 2/4）。
    /// Prefab 与 Sprite 两路并行，均完成或超时后回调 <see cref="BattleSession.BuildViewAndStart"/>。
    /// </summary>
    public static class BattleViewAssetPreloader
    {
        public static void Run(BaseBattle battle, Action onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            if (battle == null)
            {
                BattleFlowLog.Abort("BattleViewAssetPreloader.Run", "battle=null，仍触发 onComplete");
                onComplete?.Invoke();
                return;
            }

            // 两批 AB 均结束才进入 BuildViewAndStart
            int pending = 2;
            void OnOneBatchFinished()
            {
                if (pending <= 0)
                {
                    return;
                }

                pending--;
                if (pending <= 0)
                {
                    ReportCriticalMissing();
                    BattleFlowLog.Step("2/4 预加载全部完成");
                    onComplete?.Invoke();
                }
            }

            BattleViewPrefabPreloader.Run(battle, OnOneBatchFinished, timeoutSeconds);
            BattleViewSpritePreloader.Run(battle, OnOneBatchFinished, timeoutSeconds);
        }

        static void ReportCriticalMissing()
        {
            GameObject gridTemplate;
            bool hasGridPrefab = BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.Grid,
                out gridTemplate);

            Sprite gridSprite;
            bool hasGridSprite = FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridNormal, out gridSprite);

            if (hasGridPrefab && hasGridSprite)
            {
                return;
            }

            BattleFlowLog.Abort(
                "预加载关键资源检查",
                (hasGridPrefab ? string.Empty : "GridPrefab=缺失 ") +
                (hasGridSprite ? string.Empty : "GridSprite=缺失") +
                "（详见 Prefab/Sprite Preloader 失败列表）");
        }
    }
}
