using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>开战加成汇总（由 Effect 编译而来）。</summary>
    public class RoguelikeBattleModifiers
    {
        public int StartCoinBonus;
        public int TowerDamagePercentBonus;
        public readonly List<int> GlobalBuffIds = new List<int>();

        public void Clear()
        {
            this.StartCoinBonus = 0;
            this.TowerDamagePercentBonus = 0;
            this.GlobalBuffIds.Clear();
        }
    }
}
