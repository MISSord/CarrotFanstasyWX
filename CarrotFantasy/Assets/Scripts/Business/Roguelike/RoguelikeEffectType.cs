namespace CarrotFantasy
{
    /// <summary>肉鸽局外/开战全局效果类型（与单位 <see cref="BuffCategory"/> 分开）。</summary>
    public enum RoguelikeEffectType
    {
        None = 0,
        /// <summary>开战额外局内金币；Param0 = 数量。</summary>
        StartCoin = 1,
        /// <summary>塔伤害百分比加成；Param0 = 百分点数（15 = +15%）。</summary>
        TowerDamagePercent = 2,
        /// <summary>注入战斗 <c>GlobalBuffIds</c>；Param0 = TbBuff Id。</summary>
        GrantGlobalBuff = 3,
        /// <summary>仅进图时增加肉鸽金币；Param0 = 数量。</summary>
        StartingRoguelikeGold = 4,
    }
}
