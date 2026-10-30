using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图图集预加载。
    /// 只预加载图集（<see cref="AtlasLoader"/>，整图集进内存后卸 AB）；
    /// 单 Sprite/Texture（小地图、怪物头像等）不预加载，由对应模块（SpriteLoader/UIImageLoader）
    /// 在战斗准备期提前发起标准加载，底层 asset 缓存保证二次 Load 同步命中。
    /// </summary>
    public static class BattleViewSpritePreloader
    {
        struct AtlasRequest
        {
            public string Bundle;
            public string Asset;
        }

        static readonly List<AtlasHandle> AtlasHandles = new List<AtlasHandle>();
        static int preloadGeneration;

        public static bool IsReady { get; private set; }

        static bool IsPersistentAtlasBundle(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                return false;
            }

            if (string.Equals(bundleName, FightViewSpriteAb.NormalMordelAtlas, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(bundleName, FightViewSpriteAb.CarrotAtlas, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return bundleName.StartsWith("fightpart/altassprite/tower/", StringComparison.OrdinalIgnoreCase)
                || bundleName.StartsWith("fightpart/altassprite/monster/", StringComparison.OrdinalIgnoreCase)
                || bundleName.StartsWith("fightpart/altassprite/items/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bundleName, FightViewSpriteAb.EffectAtlas, StringComparison.OrdinalIgnoreCase);
        }

        public static void Run(BaseBattle battle, Action<bool> onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            int generation = ++preloadGeneration;
            List<AtlasRequest> requests = BuildRequests(battle);
            if (requests.Count == 0)
            {
                IsReady = true;
                onComplete?.Invoke(true);
                return;
            }

            MarkPersistentAtlases(requests);

            BattleViewPreloadWait wait = new BattleViewPreloadWait(
                "BattleViewSpritePreloader",
                timeoutSeconds,
                success =>
                {
                    IsReady = success;
                    onComplete?.Invoke(success);
                });

            int trackedCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                AtlasRequest req = requests[i];
                wait.Track(req.Bundle, req.Asset);
                trackedCount++;
                BeginAtlasAcquire(generation, wait, req);
            }

            if (trackedCount <= 0)
            {
                IsReady = true;
                onComplete?.Invoke(true);
                return;
            }

            wait.Start();
        }

        static void MarkPersistentAtlases(List<AtlasRequest> requests)
        {
            var marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < requests.Count; i++)
            {
                string bundle = requests[i].Bundle;
                if (!IsPersistentAtlasBundle(bundle))
                {
                    continue;
                }

                if (!marked.Add(bundle))
                {
                    continue;
                }

                AtlasLoader.Instance.SetResident(bundle, true);
            }
        }

        static void BeginAtlasAcquire(int generation, BattleViewPreloadWait wait, AtlasRequest req)
        {
            AtlasHandle handle = AtlasLoader.Instance.Load(req.Bundle, req.Asset, LoadPriority.Medium);
            AtlasHandles.Add(handle);
            handle.Completed += sprite =>
            {
                if (generation != preloadGeneration)
                {
                    return;
                }

                wait.NotifyFinished(req.Bundle, req.Asset, sprite != null);
            };
        }

        /// <summary>图集 Sprite 同步就绪查询（整图集已进内存时命中）。单 Sprite 不缓存，调用方应走 loader 异步加载。</summary>
        public static bool TryGetSprite(string bundleName, string assetName, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            return AtlasLoader.Instance.IsAtlasBundle(bundleName) &&
                   AtlasLoader.Instance.TryPeekSprite(bundleName, assetName, out sprite);
        }

        public static void Clear()
        {
            IsReady = false;
            preloadGeneration++;

            for (int i = AtlasHandles.Count - 1; i >= 0; i--)
            {
                // 常驻图集由 SetResident 留在内存，句柄仍要 Release，避免每局把引用加上去。
                AtlasHandles[i]?.Release();
                AtlasHandles.RemoveAt(i);
            }
        }

        static List<AtlasRequest> BuildRequests(BaseBattle battle)
        {
            var list = new List<AtlasRequest>();
            var dedupe = new HashSet<string>(StringComparer.Ordinal);

            void Add(string bundle, string asset)
            {
                if (string.IsNullOrEmpty(bundle) || string.IsNullOrEmpty(asset))
                {
                    return;
                }

                string key = bundle + "|" + asset;
                if (!dedupe.Add(key))
                {
                    return;
                }

                list.Add(new AtlasRequest { Bundle = bundle, Asset = asset });
            }

            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.GridNormal);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.GridStart);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.GridCantBuild);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.BtnCantUpLevel);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.BtnCanUpLevel);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.BtnReachHighestLevel);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.PausePlaying);
            Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.PausePaused);

            for (int i = 0; i <= 6; i++)
            {
                Add(FightViewSpriteAb.CarrotAtlas, FightViewSpriteAb.CarrotStateAsset(i));
            }

            BattleTowerComponent towerComponent =
                battle.GetComponent(BattleComponentType.TowerComponent) as BattleTowerComponent;
            if (towerComponent != null && towerComponent.canBuildTowerList != null)
            {
                for (int i = 0; i < towerComponent.canBuildTowerListLength; i++)
                {
                    int towerId = towerComponent.canBuildTowerList[i];
                    Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.TowerCanClickAsset(towerId, false));
                    Add(FightViewSpriteAb.NormalMordelAtlas, FightViewSpriteAb.TowerCanClickAsset(towerId, true));
                }
            }

            return list;
        }
    }
}
