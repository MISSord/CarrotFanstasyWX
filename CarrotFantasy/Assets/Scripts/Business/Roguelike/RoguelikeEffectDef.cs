namespace CarrotFantasy
{
    /// <summary>肉鸽效果静态定义（被 Item / 开局配置引用）。</summary>
    public class RoguelikeEffectDef
    {
        public int id;
        public string displayName;
        public RoguelikeEffectType type;
        /// <summary>主参数：金币数、塔伤百分点、或 TbBuff Id。</summary>
        public int param0;
    }
}
