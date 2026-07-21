namespace CarrotFantasy
{
    /// <summary>肉鸽道具静态配置（商店售卖 / 背包持有）。效果数值在 <see cref="RoguelikeEffectDef"/>。</summary>
    public class RoguelikeItemDef
    {
        public int id;
        public string displayName;
        public int price;
        /// <summary>持有上限；1 = 买过即 soldOut。</summary>
        public int maxOwn;
        /// <summary>引用的效果 id 列表。</summary>
        public int[] effectIds;
    }
}
