using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>商店货架：按 shopPointId 提供可购道具 id 列表（Phase 1 写死）。</summary>
    public class RoguelikeShopConfigReader
    {
        private static RoguelikeShopConfigReader instance;
        private readonly Dictionary<int, int[]> offersByShopPoint = new Dictionary<int, int[]>();

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
            if (this.offersByShopPoint.Count > 0)
            {
                return;
            }

            int[] defaultShelf = { 1001, 1002, 1003 };
            this.offersByShopPoint[0] = defaultShelf;
        }

        public int[] GetItemIdsForShop(int shopPointId)
        {
            int[] shelf;
            if (this.offersByShopPoint.TryGetValue(shopPointId, out shelf))
            {
                return shelf;
            }
            return this.offersByShopPoint[0];
        }
    }
}
