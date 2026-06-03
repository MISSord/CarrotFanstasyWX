namespace CarrotFantasy
{
    /// <summary>肉鸽道具静态配置（商店售卖 / 背包持有）。</summary>
    public class RoguelikeItemDef
    {
        public int id;
        public string displayName;
        public int price;
        /// <summary>进战斗时额外局内起始金币（走 <see cref="BattleEvent.COIN_CHANGE"/>）。</summary>
        public int startBattleCoinBonus;
        /// <summary>塔伤害百分比加成（100 = +100%），Phase 1 仅记录，供后续战斗公式读取。</summary>
        public int towerDamagePercentBonus;
    }
}
