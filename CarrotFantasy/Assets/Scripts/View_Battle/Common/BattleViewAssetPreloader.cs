using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图资源预加载：Prefab 与 Sprite 异步完成或超时后回调。
    /// 仅通过 <see cref="AssetLoadManager"/> 等待 AB 回调，不干预 <see cref="AssetBundleManager"/>。
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

            BattleFlowLog.Step("2/4 预加载开始", "timeout=" + timeoutSeconds + "s");

            int pending = 2;
            void OnOneBatchFinished(string batchName)
            {
                if (pending <= 0)
                {
                    BattleFlowLog.Step("预加载批次重复回调", batchName);
                    return;
                }

                pending--;
                BattleFlowLog.Step("预加载批次完成", batchName + " remaining=" + pending);

                if (pending <= 0)
                {
                    ReportCriticalMissing();
                    BattleFlowLog.Step("2/4 预加载全部完成", "即将进入 BuildViewAndStart");
                    onComplete?.Invoke();
                }
            }

            BattleViewPrefabPreloader.Run(battle, () => OnOneBatchFinished("Prefab"), timeoutSeconds);
            BattleViewSpritePreloader.Run(battle, () => OnOneBatchFinished("Sprite"), timeoutSeconds);
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
                BattleFlowLog.Step(
                    "预加载关键资源检查",
                    "GridPrefab=OK GridSprite=OK");
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
