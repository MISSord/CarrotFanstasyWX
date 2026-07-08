using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图资源预加载（由 <see cref="BattleAssetScope"/> 驱动）。
    /// Prefab 与 Sprite 两路并行，均成功且关键资源就绪后回调 onSuccess；否则 onFailure。
    /// </summary>
    public static class BattleViewAssetPreloader
    {
        public static void Run(
            BaseBattle battle,
            Action onSuccess,
            Action onFailure,
            float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            if (battle == null)
            {
                BattleFlowLog.Abort("BattleViewAssetPreloader.Run", "battle=null");
                onFailure?.Invoke();
                return;
            }

            int pending = 2;
            bool anyFailed = false;

            void OnOneBatchFinished(bool success)
            {
                if (!success)
                {
                    anyFailed = true;
                }

                pending--;
                if (pending > 0)
                {
                    return;
                }

                if (anyFailed || !HasCriticalAssets())
                {
                    ReportCriticalMissing();
                    BattleFlowLog.Abort("2/4 预加载", "资源未全部就绪");
                    onFailure?.Invoke();
                    return;
                }

                BattleFlowLog.Step("2/4 预加载全部完成");
                onSuccess?.Invoke();
            }

            BattleViewPrefabPreloader.Run(battle, OnOneBatchFinished, timeoutSeconds);
            BattleViewSpritePreloader.Run(battle, OnOneBatchFinished, timeoutSeconds);
        }

        public static bool HasCriticalAssets()
        {
            GameObject gridTemplate;
            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.Grid,
                out gridTemplate))
            {
                return false;
            }

            Sprite gridSprite;
            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridNormal, out gridSprite))
            {
                return false;
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridStart, out gridSprite))
            {
                return false;
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.GridCantBuild, out gridSprite))
            {
                return false;
            }

            GameObject hpSliderTemplate;
            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.HpSlider,
                out hpSliderTemplate))
            {
                return false;
            }

            GameObject damageFloatTemplate;
            return BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.DamageFloatText,
                out damageFloatTemplate);
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

            GameObject hpSliderTemplate;
            bool hasHpSlider = BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.HpSlider,
                out hpSliderTemplate);

            GameObject damageFloatTemplate;
            bool hasDamageFloat = BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.DamageFloatText,
                out damageFloatTemplate);

            if (hasGridPrefab && hasGridSprite && hasHpSlider && hasDamageFloat)
            {
                return;
            }

            BattleFlowLog.Abort(
                "预加载关键资源检查",
                (hasGridPrefab ? string.Empty : "GridPrefab=缺失 ") +
                (hasGridSprite ? string.Empty : "GridSprite=缺失 ") +
                (hasHpSlider ? string.Empty : "HPSlider=缺失 ") +
                (hasDamageFloat ? string.Empty : "DamageFloatText=缺失") +
                "（详见 Prefab/Sprite Preloader 失败列表）");
        }
    }
}
