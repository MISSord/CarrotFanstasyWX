using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图预制体异步预加载。
    /// 公共包（fightpart_prefab、fightview）跨局保留；塔/子弹/道具按关加载并在离关时释放。
    /// 模板经 <see cref="PrefabResourceManager"/> Load/Unload 成对管理。
    /// </summary>
    public static class BattleViewPrefabPreloader
    {
        struct PrefabRequest
        {
            public string Bundle;
            public string Asset;
        }

        struct TrackedHandle
        {
            public string Bundle;
            public int HandleId;
        }

        static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        static readonly List<TrackedHandle> Handles = new List<TrackedHandle>();
        static int preloadGeneration;

        public static bool IsReady { get; private set; }

        public static string MakeKey(string bundleName, string assetName)
        {
            return PrefabResourceManager.MakeKey(bundleName, assetName);
        }

        static bool IsTemplateAlive(GameObject template)
        {
            return template != null;
        }

        /// <summary>全关卡共用的战斗预制体包，离关时不 Unload。</summary>
        static bool IsPersistentPrefabBundle(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                return false;
            }

            return string.Equals(bundleName, FightViewPrefabAb.FightPartBundle, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(bundleName, FightViewPrefabAb.FightViewBundle, StringComparison.OrdinalIgnoreCase);
        }

        static string GetBundleNameFromTemplateKey(string templateKey)
        {
            if (string.IsNullOrEmpty(templateKey))
            {
                return string.Empty;
            }

            int sep = templateKey.IndexOf('|');
            return sep > 0 ? templateKey.Substring(0, sep) : templateKey;
        }

        public static void Run(BaseBattle battle, Action<bool> onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            int generation = ++preloadGeneration;
            List<PrefabRequest> requests = BuildRequests(battle);
            if (requests.Count == 0)
            {
                IsReady = true;
                onComplete?.Invoke(true);
                return;
            }

            BattleViewPreloadWait wait = new BattleViewPreloadWait(
                "BattleViewPrefabPreloader",
                timeoutSeconds,
                success =>
                {
                    IsReady = success;
                    onComplete?.Invoke(success);
                });

            int trackedCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                PrefabRequest req = requests[i];
                string key = MakeKey(req.Bundle, req.Asset);
                GameObject cached;
                if (Templates.TryGetValue(key, out cached) && IsTemplateAlive(cached))
                {
                    continue;
                }

                if (cached != null)
                {
                    Templates.Remove(key);
                }

                wait.Track(req.Bundle, req.Asset);
                trackedCount++;

                int handleId = PrefabResourceManager.Instance.Load(
                    req.Bundle,
                    req.Asset,
                    go =>
                    {
                        if (generation != preloadGeneration)
                        {
                            return;
                        }

                        if (go != null)
                        {
                            Templates[key] = go;
                        }

                        wait.NotifyFinished(req.Bundle, req.Asset, go != null);
                    },
                    LoadPriority.Medium);

                if (handleId != PrefabResourceManager.InvalidHandle)
                {
                    Handles.Add(new TrackedHandle
                    {
                        Bundle = req.Bundle,
                        HandleId = handleId,
                    });
                }
                else
                {
                    wait.NotifyFinished(req.Bundle, req.Asset, false);
                }
            }

            if (trackedCount <= 0)
            {
                IsReady = true;
                onComplete?.Invoke(true);
                return;
            }

            wait.Start();
        }

        public static bool TryGetTemplate(string bundleName, string assetName, out GameObject template)
        {
            template = null;
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            GameObject go;
            if (!Templates.TryGetValue(MakeKey(bundleName, assetName), out go) || go == null)
            {
                return false;
            }

            template = go;
            return true;
        }

        public static void Clear()
        {
            IsReady = false;
            preloadGeneration++;

            for (int i = Handles.Count - 1; i >= 0; i--)
            {
                TrackedHandle tracked = Handles[i];
                if (IsPersistentPrefabBundle(tracked.Bundle))
                {
                    continue;
                }

                PrefabResourceManager.Instance.Unload(tracked.HandleId);
                Handles.RemoveAt(i);
            }

            List<string> removeKeys = null;
            foreach (KeyValuePair<string, GameObject> pair in Templates)
            {
                if (IsPersistentPrefabBundle(GetBundleNameFromTemplateKey(pair.Key)))
                {
                    continue;
                }

                if (removeKeys == null)
                {
                    removeKeys = new List<string>();
                }

                removeKeys.Add(pair.Key);
            }

            if (removeKeys != null)
            {
                for (int i = 0; i < removeKeys.Count; i++)
                {
                    Templates.Remove(removeKeys[i]);
                }
            }
        }

        static List<PrefabRequest> BuildRequests(BaseBattle battle)
        {
            var list = new List<PrefabRequest>();
            var dedupe = new HashSet<string>(StringComparer.Ordinal);

            void Add(string bundle, string asset)
            {
                if (string.IsNullOrEmpty(bundle) || string.IsNullOrEmpty(asset))
                {
                    return;
                }

                string key = MakeKey(bundle, asset);
                if (!dedupe.Add(key))
                {
                    return;
                }

                list.Add(new PrefabRequest { Bundle = bundle, Asset = asset });
            }

            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.Grid);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterPrefab);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.HpSlider);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DamageFloatText);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.BuildEffect);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeMap);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeTargetSignal);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.StartPoint);
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.Carrot);

            Add(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.TowerList);
            Add(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.BtnTowerBuild);
            Add(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.HandleTowerCanvas);

            int bigLevel = 1;
            BattlePVEDataComponent pveData = BattlePVEDataComponent.GetFrom(battle);
            if (pveData != null)
            {
                bigLevel = pveData.bigLevel;
            }

            BattleTowerComponent towerComponent = battle.GetComponent(BattleComponentType.TowerComponent) as BattleTowerComponent;
            if (towerComponent != null && towerComponent.canBuildTowerList != null)
            {
                for (int i = 0; i < towerComponent.canBuildTowerListLength; i++)
                {
                    int towerId = towerComponent.canBuildTowerList[i];
                    for (int level = 1; level <= 3; level++)
                    {
                        Add(FightViewPrefabAb.FightPartTowerBundle, FightViewPrefabAb.TowerAssetName(towerId, level));
                        Add(FightViewPrefabAb.FightPartBulletBundle, FightViewPrefabAb.BulletAssetName(towerId, level));
                    }
                }
            }

            BattleItemComponent itemComponent = battle.GetComponent(BattleComponentType.ItemComponent) as BattleItemComponent;
            if (itemComponent != null)
            {
                for (int i = 0; i < itemComponent.battleItemList.Count; i++)
                {
                    int itemId = itemComponent.battleItemList[i].itemId;
                    Add(FightViewPrefabAb.FightPartItemBundle, FightViewPrefabAb.ItemAssetName(bigLevel, itemId));
                }
            }

            return list;
        }
    }
}
