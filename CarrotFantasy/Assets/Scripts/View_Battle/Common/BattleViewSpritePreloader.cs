using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗视图 Sprite 异步预加载，替代各 BV 组件内 <see cref="ResourceLoader.loadRes"/>。</summary>
    public static class BattleViewSpritePreloader
    {
        struct SpriteRequest
        {
            public string Bundle;
            public string Asset;
        }

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static readonly List<AssetLoadHandle> Handles = new List<AssetLoadHandle>();

        public static bool IsReady { get; private set; }

        public static string MakeKey(string bundleName, string assetName)
        {
            return bundleName + "|" + assetName;
        }

        static bool IsSpriteAlive(Sprite sprite)
        {
            return sprite != null;
        }

        public static void Run(BaseBattle battle, Action onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            List<SpriteRequest> requests = BuildRequests(battle);
            if (requests.Count == 0)
            {
                IsReady = true;
                onComplete?.Invoke();
                return;
            }

            BattleViewPreloadWait wait = new BattleViewPreloadWait(
                "BattleViewSpritePreloader",
                timeoutSeconds,
                () =>
                {
                    IsReady = true;
                    onComplete?.Invoke();
                });

            int trackedCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                SpriteRequest req = requests[i];
                string key = MakeKey(req.Bundle, req.Asset);
                Sprite cached;
                if (Sprites.TryGetValue(key, out cached))
                {
                    if (IsSpriteAlive(cached))
                    {
                        continue;
                    }

                    Sprites.Remove(key);
                }

                wait.Track(req.Bundle, req.Asset);
                trackedCount++;
                AssetLoadHandle handle = ImageResourceManager.Instance.LoadSprite(
                    req.Bundle,
                    req.Asset,
                    sprite =>
                    {
                        if (sprite != null)
                        {
                            Sprites[key] = sprite;
                        }

                        wait.NotifyFinished(req.Bundle, req.Asset, sprite != null);
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

        public static bool TryGetSprite(string bundleName, string assetName, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            Sprite loaded;
            if (!Sprites.TryGetValue(MakeKey(bundleName, assetName), out loaded) || loaded == null)
            {
                return false;
            }

            sprite = loaded;
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
            Sprites.Clear();
        }

        static List<SpriteRequest> BuildRequests(BaseBattle battle)
        {
            var list = new List<SpriteRequest>();
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

                list.Add(new SpriteRequest { Bundle = bundle, Asset = asset });
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
                    string towerBundle = FightViewSpriteAb.TowerAtlasBundle(towerId);
                    Add(towerBundle, FightViewSpriteAb.TowerCanClick0);
                    Add(towerBundle, FightViewSpriteAb.TowerCanClick1);
                }
            }

            BattlePVEDataComponent pveData = BattlePVEDataComponent.GetFrom(battle);
            if (pveData != null)
            {
                var reader = new MapUIConfigReader();
                reader.Init();
                Dictionary<string, int> map;
                reader.TryGetMapUIConfig(pveData.bigLevel, pveData.level, out map);
                if (map != null)
                {
                    int bgIndex;
                    int roadIndex;
                    if (!map.TryGetValue("mapBg", out bgIndex))
                    {
                        bgIndex = 0;
                    }

                    if (!map.TryGetValue("mapRoad", out roadIndex))
                    {
                        roadIndex = 1;
                    }

                    string bgAsset = FightViewSpriteAb.MapBgAssetName(bgIndex);
                    Add(FightViewSpriteAb.RawImageBundle(bgAsset), bgAsset);

                    string roadAsset = FightViewSpriteAb.MapRoadAssetName(roadIndex);
                    Add(FightViewSpriteAb.RawImageBundle(roadAsset), roadAsset);
                }
            }

            CollectLevelMonsterPortraits(battle, Add);

            return list;
        }

        static void CollectLevelMonsterPortraits(BaseBattle battle, Action<string, string> add)
        {
            LevelInfo levelInfo = ResolveLevelInfo(battle);
            if (levelInfo == null || levelInfo.roundInfo == null)
            {
                return;
            }

            var monsterIds = new HashSet<int>();
            for (int i = 0; i < levelInfo.roundInfo.Count; i++)
            {
                WaveSpawnPlan plan = SpawnPlanCompiler.Compile(levelInfo.roundInfo[i]);
                for (int j = 0; j < plan.Count; j++)
                {
                    monsterIds.Add(plan.MonsterIds[j]);
                }
            }

            foreach (int monsterId in monsterIds)
            {
                string assetName = FightViewSpriteAb.MonsterPortraitAssetName(monsterId);
                add(FightViewSpriteAb.RawImageBundle(assetName), assetName);
            }
        }

        static LevelInfo ResolveLevelInfo(BaseBattle battle)
        {
            IBattleMapLevelData mapData =
                battle.GetComponent(BattleComponentType.MapComponent) as IBattleMapLevelData;
            return mapData != null ? mapData.LevelInfo : null;
        }
    }
}
