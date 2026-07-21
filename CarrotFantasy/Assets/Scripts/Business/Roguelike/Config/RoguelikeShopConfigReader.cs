using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>商店货架池（优先读 Luban <c>TbRoguelikeShopPool</c>）。</summary>
    public class RoguelikeShopConfigReader
    {
        private static RoguelikeShopConfigReader instance;
        private readonly Dictionary<int, RoguelikeShopPoolDef> pools = new Dictionary<int, RoguelikeShopPoolDef>();

        public static RoguelikeShopConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoguelikeShopConfigReader();
                    instance.Init();
                }
                return instance;
            }
        }

        public void Init()
        {
            this.pools.Clear();
            RoguelikeItemConfigReader.Instance.Init();
            if (this.TryLoadFromLuban())
            {
                return;
            }

            Debug.LogWarning("[RoguelikeShopConfigReader] Luban empty, using hardcoded fallback.");
            this.LoadFallback();
        }

        bool TryLoadFromLuban()
        {
            try
            {
                var table = LubanConfigLoader.Tables.TbRoguelikeShopPool;
                if (table == null || table.DataList == null || table.DataList.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < table.DataList.Count; i++)
                {
                    cfg.RoguelikeShopPoolDef src = table.DataList[i];
                    var def = new RoguelikeShopPoolDef
                    {
                        poolId = src.PoolId,
                        pickCount = src.PickCount,
                    };
                    int n = src.ItemIds != null ? src.ItemIds.Count : 0;
                    for (int k = 0; k < n; k++)
                    {
                        int weight = (src.Weights != null && k < src.Weights.Count)
                            ? src.Weights[k]
                            : 1;
                        def.entries.Add(new RoguelikeShopPoolEntry
                        {
                            itemId = src.ItemIds[k],
                            weight = weight,
                        });
                    }
                    this.pools[def.poolId] = def;
                }
                return this.pools.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RoguelikeShopConfigReader] Luban load failed: " + ex.Message);
                return false;
            }
        }

        void LoadFallback()
        {
            this.AddPool(0, 0, Entry(1001, 50), Entry(1002, 30), Entry(1003, 10), Entry(1004, 10));
            this.AddPool(1, 0, Entry(1001, 50), Entry(1002, 30), Entry(1003, 10));
            this.AddPool(2, 0, Entry(1001, 40), Entry(1002, 30), Entry(1003, 20), Entry(1004, 10));
            this.AddPool(3, 0, Entry(1002, 40), Entry(1003, 30), Entry(1004, 30));
        }

        static RoguelikeShopPoolEntry Entry(int itemId, int weight)
        {
            return new RoguelikeShopPoolEntry { itemId = itemId, weight = weight };
        }

        void AddPool(int poolId, int pickCount, params RoguelikeShopPoolEntry[] entries)
        {
            var def = new RoguelikeShopPoolDef
            {
                poolId = poolId,
                pickCount = pickCount,
            };
            if (entries != null)
            {
                def.entries.AddRange(entries);
            }
            this.pools[poolId] = def;
        }

        public bool TryGetPool(int shopPoolId, out RoguelikeShopPoolDef def)
        {
            return this.pools.TryGetValue(shopPoolId, out def);
        }

        public int[] ResolveShelfItemIds(int shopPoolId, int seed = 0)
        {
            RoguelikeShopPoolDef pool;
            if (!this.TryGetPool(shopPoolId, out pool) || pool.entries == null || pool.entries.Count == 0)
            {
                if (!this.TryGetPool(0, out pool) || pool.entries == null)
                {
                    return System.Array.Empty<int>();
                }
            }

            int count = pool.entries.Count;
            int pick = pool.pickCount;
            if (pick <= 0 || pick >= count)
            {
                var all = new int[count];
                for (int i = 0; i < count; i++)
                {
                    all[i] = pool.entries[i].itemId;
                }
                return all;
            }

            return WeightedSample(pool.entries, pick, seed);
        }

        public int[] GetItemIdsForShopPool(int shopPoolId)
        {
            return this.ResolveShelfItemIds(shopPoolId, 0);
        }

        public int[] GetItemIdsForShop(int shopPointId)
        {
            return this.GetItemIdsForShopPool(0);
        }

        static int[] WeightedSample(List<RoguelikeShopPoolEntry> entries, int pickCount, int seed)
        {
            var remaining = new List<RoguelikeShopPoolEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                remaining.Add(entries[i]);
            }

            var rng = new System.Random(seed != 0 ? seed : 1);
            var result = new int[pickCount];
            for (int n = 0; n < pickCount; n++)
            {
                int totalWeight = 0;
                for (int i = 0; i < remaining.Count; i++)
                {
                    totalWeight += Mathf.Max(1, remaining[i].weight);
                }

                int roll = rng.Next(totalWeight);
                int chosen = 0;
                for (int i = 0; i < remaining.Count; i++)
                {
                    roll -= Mathf.Max(1, remaining[i].weight);
                    if (roll < 0)
                    {
                        chosen = i;
                        break;
                    }
                }

                result[n] = remaining[chosen].itemId;
                remaining.RemoveAt(chosen);
            }

            return result;
        }
    }
}
