using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗视图预制体异步预加载</summary>
    public static class BattleViewPrefabPreloader
    {
        struct PrefabRequest
        {
            public string Bundle;
            public string Asset;
        }

        static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        static readonly List<AssetLoadHandle> Handles = new List<AssetLoadHandle>();

        public static bool IsReady { get; private set; }

        public static string MakeKey(string bundleName, string assetName)
        {
            return bundleName + "|" + assetName;
        }

        static bool IsTemplateAlive(GameObject template)
        {
            return template != null;
        }

        public static void Run(BaseBattle battle, Action onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            List<PrefabRequest> requests = BuildRequests(battle);
            if (requests.Count == 0)
            {
                IsReady = true;
                onComplete?.Invoke();
                return;
            }

            BattleViewPreloadWait wait = new BattleViewPreloadWait(
                "BattleViewPrefabPreloader",
                timeoutSeconds,
                () =>
                {
                    IsReady = true;
                    onComplete?.Invoke();
                });

            int trackedCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                PrefabRequest req = requests[i];
                string key = MakeKey(req.Bundle, req.Asset);
                GameObject cached;
                if (Templates.TryGetValue(key, out cached))
                {
                    if (IsTemplateAlive(cached))
                    {
                        continue;
                    }

                    Templates.Remove(key);
                }

                wait.Track(req.Bundle, req.Asset);
                trackedCount++;
                AssetLoadHandle handle = GameObjectResourceManager.Instance.LoadPrefab(
                    req.Bundle,
                    req.Asset,
                    go =>
                    {
                        if (go != null)
                        {
                            Templates[key] = go;
                        }

                        wait.NotifyFinished(req.Bundle, req.Asset, go != null);
                    },
                    LoadPriority.Medium);

                if (handle.IsValid)
                {
                    Handles.Add(handle);
                }
                else
                {
                    wait.NotifyFinished(req.Bundle, req.Asset, false);
                }
            }

            if (trackedCount <= 0)
            {
                IsReady = true;
                onComplete?.Invoke();
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
            for (int i = 0; i < Handles.Count; i++)
            {
                Handles[i].Dispose();
            }
            Handles.Clear();
            Templates.Clear();
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
            Add(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterCanvas);
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
                        Add(FightViewPrefabAb.FightPartEffectBundle, FightViewPrefabAb.EffectAssetName(towerId, level));
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
