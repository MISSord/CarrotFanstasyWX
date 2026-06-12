namespace CarrotFantasy
{
    /// <summary>本局战斗全局 Buff 汇总快照（开战时编译，局内只读）。</summary>
    public sealed class BattleGlobalBuffSnapshot
    {
        public int StartCoinBonus;
        public int TowerDamagePercentBonus;

        public Fix64 GetTowerDamageMultiplier()
        {
            if (this.TowerDamagePercentBonus <= 0)
            {
                return Fix64.One;
            }

            return Fix64.One + new Fix64(this.TowerDamagePercentBonus) / new Fix64(100);
        }
    }
}
