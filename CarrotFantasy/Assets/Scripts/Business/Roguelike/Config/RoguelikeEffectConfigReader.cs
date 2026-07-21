using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>肉鸽效果表（优先读 Luban <c>TbRoguelikeEffect</c>）。</summary>
    public class RoguelikeEffectConfigReader
    {
        private static RoguelikeEffectConfigReader instance;
        private readonly Dictionary<int, RoguelikeEffectDef> defs = new Dictionary<int, RoguelikeEffectDef>();

        public static RoguelikeEffectConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoguelikeEffectConfigReader();
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
                return;
            }

            Debug.LogWarning("[RoguelikeEffectConfigReader] Luban empty, using hardcoded fallback.");
            this.LoadFallback();
        }

        bool TryLoadFromLuban()
        {
            try
            {
                var table = LubanConfigLoader.Tables.TbRoguelikeEffect;
                if (table == null || table.DataList == null || table.DataList.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < table.DataList.Count; i++)
                {
                    cfg.RoguelikeEffectDef src = table.DataList[i];
                    this.defs[src.EffectId] = new RoguelikeEffectDef
                    {
                        id = src.EffectId,
                        displayName = src.Name,
                        type = (RoguelikeEffectType)(int)src.Type,
                        param0 = src.Param0,
                    };
                }
                return this.defs.Count > 0;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RoguelikeEffectConfigReader] Luban load failed: " + ex.Message);
                return false;
            }
        }

        void LoadFallback()
        {
            Add(2001, "开战金币+200", RoguelikeEffectType.StartCoin, 200);
            Add(2002, "塔伤+15%", RoguelikeEffectType.TowerDamagePercent, 15);
            Add(2003, "开战金币+100", RoguelikeEffectType.StartCoin, 100);
            Add(2004, "塔伤+10%", RoguelikeEffectType.TowerDamagePercent, 10);
            Add(2005, "注入全局Buff(Slow#1001)", RoguelikeEffectType.GrantGlobalBuff, 1001);
            Add(2006, "进图肉鸽金+50", RoguelikeEffectType.StartingRoguelikeGold, 50);
        }

        void Add(int id, string name, RoguelikeEffectType type, int param0)
        {
            this.defs[id] = new RoguelikeEffectDef
            {
                id = id,
                displayName = name,
                type = type,
                param0 = param0,
            };
        }

        public bool TryGet(int effectId, out RoguelikeEffectDef def)
        {
            return this.defs.TryGetValue(effectId, out def);
        }
    }
}
