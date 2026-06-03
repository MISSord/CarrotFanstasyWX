namespace CarrotFantasy
{
    /// <summary>当前单局战斗从 Run 背包汇总的数值加成（战斗开始时写入，结束后清零）。</summary>
    public static class RoguelikeBattleModifiers
    {
        public static int TowerDamagePercentBonus { get; private set; }

        public static void ApplyFromRun()
        {
            TowerDamagePercentBonus = 0;
            if (!RoguelikeRunServer.Instance.IsRunActive)
            {
                return;
            }
            RoguelikeRunServer.Instance.CollectBattleModifiers(out _, out int towerBonus);
            TowerDamagePercentBonus = towerBonus;
        }

        public static void Clear()
        {
            TowerDamagePercentBonus = 0;
        }
    }
}
