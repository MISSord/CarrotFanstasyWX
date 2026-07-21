using System.Collections.Generic;

namespace CarrotFantasy
{
    public class RoguelikeShopPoolEntry
    {
        public int itemId;
        public int weight;
    }

    public class RoguelikeShopPoolDef
    {
        public int poolId;
        /// <summary>每次进店抽几件；≤0 或 ≥候选数时取全部（固定货架）。</summary>
        public int pickCount;
        public List<RoguelikeShopPoolEntry> entries = new List<RoguelikeShopPoolEntry>();
    }
}
