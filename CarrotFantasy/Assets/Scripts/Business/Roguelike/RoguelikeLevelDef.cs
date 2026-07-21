namespace CarrotFantasy
{
    /// <summary>肉鸽小关静态配置（选关进图时消费，不随局内变化）。</summary>
    public class RoguelikeLevelDef
    {
        public int bigLevelId;
        public int levelId;
        public string displayName;

        /// <summary>Hex 大地图资源名（Phase 1 场景内仍用手挂 mapAsset，此字段供后续动态加载）。</summary>
        public string hexMapAssetId;

        /// <summary>商店货架池 id，供 <see cref="RoguelikeShopConfigReader"/> 解析。</summary>
        public int shopPoolId;

        /// <summary>开局肉鸽金币；≤0 时用 Run 默认值。</summary>
        public int startingGold;

        /// <summary>开局自带效果 id（不进商店背包，开战时计入加成）。</summary>
        public int[] startingEffectIds;

        /// <summary>遭遇表 id（encounterId → 战斗关卡映射，后续用）。</summary>
        public int encounterTableId;

        /// <summary>随机事件池 id（后续用）。</summary>
        public int randomEventPoolId;
    }
}
