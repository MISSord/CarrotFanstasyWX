using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>肉鸽道具表（优先读 Luban <c>TbRoguelikeItem</c>）。</summary>
    public class RoguelikeItemConfigReader
    {
        private static RoguelikeItemConfigReader instance;
        private readonly Dictionary<int, RoguelikeItemDef> defs = new Dictionary<int, RoguelikeItemDef>();

        public static RoguelikeItemConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoguelikeItemConfigReader();
                    instance.Init();
                }
                return instance;
            }
        }

        public void Init()
        {
            this.defs.Clear();
            RoguelikeEffectConfigReader.Instance.Init();
            if (this.TryLoadFromLuban())
            {
                return;
            }

            Debug.LogWarning("[RoguelikeItemConfigReader] Luban empty, using hardcoded fallback.");
            this.LoadFallback();
        }

        bool TryLoadFromLuban()
        {
            try
            {
                var table = LubanConfigLoader.Tables.TbRoguelikeItem;
                if (table == null || table.DataList == null || table.DataList.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < table.DataList.Count; i++)
                {
                    cfg.RoguelikeItemDef src = table.DataList[i];
                    this.defs[src.ItemId] = new RoguelikeItemDef
                    {
                        id = src.ItemId,
                        displayName = src.Name,
                        price = src.Price,
                        maxOwn = src.MaxOwn <= 0 ? 1 : src.MaxOwn,
                        effectIds = src.EffectIds != null ? src.EffectIds.ToArray() : System.Array.Empty<int>(),
                    };
                }
                return this.defs.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RoguelikeItemConfigReader] Luban load failed: " + ex.Message);
                return false;
            }
        }

        void LoadFallback()
        {
            Add(1001, "开局资金+", 80, 1, new[] { 2001 });
            Add(1002, "塔伤强化", 120, 1, new[] { 2002 });
            Add(1003, "全面增益", 200, 1, new[] { 2003, 2004 });
            Add(1004, "毒弹补给", 150, 1, new[] { 2005 });
        }

        void Add(int id, string name, int price, int maxOwn, int[] effects)
        {
            this.defs[id] = new RoguelikeItemDef
            {
                id = id,
                displayName = name,
                price = price,
                maxOwn = maxOwn <= 0 ? 1 : maxOwn,
                effectIds = effects ?? System.Array.Empty<int>(),
            };
        }

        public bool TryGet(int itemId, out RoguelikeItemDef def)
        {
            return this.defs.TryGetValue(itemId, out def);
        }
    }
}
