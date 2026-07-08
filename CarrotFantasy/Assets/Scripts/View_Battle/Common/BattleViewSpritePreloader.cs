using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图 Sprite 异步预加载。
    /// Atlas 资源走 <see cref="ImageResourceManager.LoadSprite"/>；
    /// RawImages 目录资源按 Texture 加载后运行时转 Sprite（与关卡 UI 的 RawImage 约定一致）。
    /// </summary>
    public static class BattleViewSpritePreloader
    {
        struct SpriteRequest
        {
            public string Bundle;
            public string Asset;
        }

        struct CachedSprite
        {
            public Sprite Sprite;
            public bool RuntimeCreated;
        }

        static readonly Dictionary<string, CachedSprite> Sprites = new Dictionary<string, CachedSprite>(StringComparer.Ordinal);
        static readonly List<AssetLoadHandle> Handles = new List<AssetLoadHandle>();

        public static bool IsReady { get; private set; }

        public static string MakeKey(string bundleName, string assetName)
        {
            return bundleName + "|" + assetName;
        }

        static bool IsRawImageBundle(string bundleName)
        {
            return !string.IsNullOrEmpty(bundleName) &&
                   bundleName.StartsWith("ui/rawimages/", StringComparison.OrdinalIgnoreCase);
        }

        static Sprite CreateSpriteFromTexture(Texture texture)
        {
            Texture2D texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                return null;
            }

            return Sprite.Create(
                texture2D,
                new Rect(0f, 0f, texture2D.width, texture2D.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        public static void Run(BaseBattle battle, Action<bool> onComplete, float timeoutSeconds = BattleViewPreloadWait.DefaultTimeoutSeconds)
        {
            List<SpriteRequest> requests = BuildRequests(battle);
            if (requests.Count == 0)
            {
                IsReady = true;
                onComplete?.Invoke(true);
                return;
            }

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
                SpriteRequest req = requests[i];
                string key = MakeKey(req.Bundle, req.Asset);
                CachedSprite cached;
                if (Sprites.TryGetValue(key, out cached))
                {
                    if (cached.Sprite != null)
                    {
                        continue;
                    }

                    Sprites.Remove(key);
                }

                wait.Track(req.Bundle, req.Asset);
                trackedCount++;

                AssetLoadHandle handle;
                if (IsRawImageBundle(req.Bundle))
                {
                    handle = ImageResourceManager.Instance.LoadTexture(
                        req.Bundle,
                        req.Asset,
                        texture =>
                        {
                            Sprite sprite = CreateSpriteFromTexture(texture);
                            if (sprite != null)
                            {
                                Sprites[key] = new CachedSprite
                                {
                                    Sprite = sprite,
                                    RuntimeCreated = true,
                                };
                            }

                            wait.NotifyFinished(req.Bundle, req.Asset, sprite != null);
                        },
                        LoadPriority.Medium);
                }
                else
                {
                    handle = ImageResourceManager.Instance.LoadSprite(
                        req.Bundle,
                        req.Asset,
                        sprite =>
                        {
                            if (sprite != null)
                            {
                                Sprites[key] = new CachedSprite
                                {
                                    Sprite = sprite,
                                    RuntimeCreated = false,
                                };
                            }

                            wait.NotifyFinished(req.Bundle, req.Asset, sprite != null);
                        },
                        LoadPriority.Medium);
                }

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
                onComplete?.Invoke(true);
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

            CachedSprite loaded;
            if (!Sprites.TryGetValue(MakeKey(bundleName, assetName), out loaded) || loaded.Sprite == null)
            {
                return false;
            }

            sprite = loaded.Sprite;
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

            foreach (KeyValuePair<string, CachedSprite> pair in Sprites)
            {
                CachedSprite entry = pair.Value;
                if (entry.RuntimeCreated && entry.Sprite != null)
                {
                    UnityEngine.Object.Destroy(entry.Sprite);
                }
            }

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
                string bgAsset = FightViewSpriteAb.MapBgAssetName(pveData.bigLevel, pveData.level);
                Add(FightViewSpriteAb.RawImageBundle(bgAsset), bgAsset);

                string roadAsset = FightViewSpriteAb.MapRoadAssetName(pveData.bigLevel, pveData.level);
                Add(FightViewSpriteAb.RawImageBundle(roadAsset), roadAsset);
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
