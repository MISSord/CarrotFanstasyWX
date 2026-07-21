using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>肉鸽小关表（优先读 Luban <c>TbRoguelikeLevel</c>）。</summary>
    public class RoguelikeLevelConfigReader
    {
        private static RoguelikeLevelConfigReader instance;
        private readonly Dictionary<int, RoguelikeLevelDef> defs = new Dictionary<int, RoguelikeLevelDef>();

        public static int MaxBigLevel { get; private set; } = 2;
        public static int LevelsPerBig { get; private set; } = 3;

        public static RoguelikeLevelConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoguelikeLevelConfigReader();
                    instance.Init();
                }
                return instance;
            }
        }

        public void Init()
        {
            this.defs.Clear();
            if (this.TryLoadFromLuban())
            {
                this.RefreshBounds();
                return;
            }

            Debug.LogWarning("[RoguelikeLevelConfigReader] Luban empty, using hardcoded fallback.");
            this.LoadFallback();
            this.RefreshBounds();
        }

        bool TryLoadFromLuban()
        {
            try
            {
                var table = LubanConfigLoader.Tables.TbRoguelikeLevel;
                if (table == null || table.DataList == null || table.DataList.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < table.DataList.Count; i++)
                {
                    cfg.RoguelikeLevelDef src = table.DataList[i];
                    this.defs[Key(src.BigLevelId, src.LevelId)] = new RoguelikeLevelDef
                    {
                        bigLevelId = src.BigLevelId,
                        levelId = src.LevelId,
                        displayName = src.DisplayName,
                        hexMapAssetId = src.HexMapAssetId,
                        shopPoolId = src.ShopPoolId,
                        startingGold = src.StartingGold,
                        startingEffectIds = src.StartingEffectIds != null
                            ? src.StartingEffectIds.ToArray()
                            : System.Array.Empty<int>(),
                        encounterTableId = src.EncounterTableId,
                        randomEventPoolId = src.RandomEventPoolId,
                    };
                }
                return this.defs.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RoguelikeLevelConfigReader] Luban load failed: " + ex.Message);
                return false;
            }
        }

        void LoadFallback()
        {
            this.Add(1, 1, "草原入门", "SampleWorldMap", 1, 100, null, 1, 1);
            this.Add(1, 2, "草原进阶", "SampleWorldMap", 1, 120, new[] { 2001 }, 1, 1);
            this.Add(1, 3, "草原挑战", "SampleWorldMap", 2, 100, new[] { 2002 }, 2, 2);
            this.Add(2, 1, "地下入门", "SampleWorldMap", 2, 100, null, 2, 2);
            this.Add(2, 2, "地下进阶", "SampleWorldMap", 3, 150, new[] { 2001, 2002 }, 3, 2);
            this.Add(2, 3, "地下终章", "SampleWorldMap", 3, 80, new[] { 2003, 2004, 2006 }, 3, 3);
        }

        void Add(
            int big,
            int level,
            string name,
            string hexMap,
            int shopPoolId,
            int startingGold,
            int[] startingEffects,
            int encounterTableId,
            int randomEventPoolId)
        {
            this.defs[Key(big, level)] = new RoguelikeLevelDef
            {
                bigLevelId = big,
                levelId = level,
                displayName = name,
                hexMapAssetId = hexMap,
                shopPoolId = shopPoolId,
                startingGold = startingGold,
                startingEffectIds = startingEffects ?? System.Array.Empty<int>(),
                encounterTableId = encounterTableId,
                randomEventPoolId = randomEventPoolId,
            };
        }

        void RefreshBounds()
        {
            int maxBig = 0;
            int maxLevel = 0;
            foreach (KeyValuePair<int, RoguelikeLevelDef> kv in this.defs)
            {
                if (kv.Value.bigLevelId > maxBig)
                {
                    maxBig = kv.Value.bigLevelId;
                }
                if (kv.Value.levelId > maxLevel)
                {
                    maxLevel = kv.Value.levelId;
                }
            }

            if (maxBig > 0)
            {
                MaxBigLevel = maxBig;
            }
            if (maxLevel > 0)
            {
                LevelsPerBig = maxLevel;
            }
        }

        static int Key(int big, int level)
        {
            return big * 100 + level;
        }

        public bool TryGet(int bigLevelId, int levelId, out RoguelikeLevelDef def)
        {
            return this.defs.TryGetValue(Key(bigLevelId, levelId), out def);
        }

        public RoguelikeLevelDef Get(int bigLevelId, int levelId)
        {
            RoguelikeLevelDef def;
            if (this.TryGet(bigLevelId, levelId, out def))
            {
                return def;
            }
            return null;
        }

        public int GetLevelCount()
        {
            return this.defs.Count;
        }
    }
}
