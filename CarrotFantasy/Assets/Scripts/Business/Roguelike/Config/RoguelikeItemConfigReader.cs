using System.Collections.Generic;

namespace CarrotFantasy
{
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
            if (this.defs.Count > 0)
            {
                return;
            }

            Add(1001, "开局资金+", 80, startBattleCoinBonus: 200, towerDamagePercentBonus: 0);
            Add(1002, "塔伤强化", 120, startBattleCoinBonus: 0, towerDamagePercentBonus: 15);
            Add(1003, "全面增益", 200, startBattleCoinBonus: 100, towerDamagePercentBonus: 10);
        }

        void Add(int id, string name, int price, int startBattleCoinBonus, int towerDamagePercentBonus)
        {
            this.defs[id] = new RoguelikeItemDef
            {
                id = id,
                displayName = name,
                price = price,
                startBattleCoinBonus = startBattleCoinBonus,
                towerDamagePercentBonus = towerDamagePercentBonus,
            };
        }

        public bool TryGet(int itemId, out RoguelikeItemDef def)
        {
            return this.defs.TryGetValue(itemId, out def);
        }
    }
}
