namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图 Sprite 的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致）。
    /// </summary>
    public static class FightViewSpriteAb
    {
        public const string NormalMordelAtlas = "ui/view/normalmordel/images_atlas";
        public const string CarrotAtlas = "ui/images/carrot/images_atlas";

        public const string GridNormal = "Grid";
        public const string GridStart = "StartSprite";
        public const string GridCantBuild = "cantBuild";

        public const string BtnCantUpLevel = "Btn_CantUpLevel";
        public const string BtnCanUpLevel = "Btn_CanUpLevel";
        public const string BtnReachHighestLevel = "Btn_ReachHighestLevel";

        public const string PausePlaying = "pause_1";
        public const string PausePaused = "pause_3";

        public const string TowerCanClick0 = "CanClick0";
        public const string TowerCanClick1 = "CanClick1";

        public static string TowerAtlasBundle(int towerId)
        {
            return string.Format("ui/images/tower/{0}/images_atlas", towerId);
        }

        public static string CarrotStateAsset(int stateIndex)
        {
            return stateIndex.ToString();
        }

        /// <summary>关卡小地图底图：Level_{bigLevel}_{level}_BG（全局唯一，避免跨章节同名冲突）。</summary>
        public static string MapBgAssetName(int bigLevel, int level)
        {
            return string.Format("Level_{0}_{1}_BG", bigLevel, level);
        }

        /// <summary>关卡小地图路径图：Level_{bigLevel}_{level}_Road。</summary>
        public static string MapRoadAssetName(int bigLevel, int level)
        {
            return string.Format("Level_{0}_{1}_Road", bigLevel, level);
        }

        public static string RawImageBundle(string assetName)
        {
            return string.Format("ui/rawimages/{0}", assetName.ToLowerInvariant());
        }

        public static string MonsterPortraitAssetName(int monsterId)
        {
            return string.Format("{0}-1", monsterId);
        }

        public static bool TryGetNormalMordel(string assetName, out UnityEngine.Sprite sprite)
        {
            return BattleViewSpritePreloader.TryGetSprite(NormalMordelAtlas, assetName, out sprite);
        }

        public static bool TryGetCarrotState(int stateIndex, out UnityEngine.Sprite sprite)
        {
            return BattleViewSpritePreloader.TryGetSprite(CarrotAtlas, CarrotStateAsset(stateIndex), out sprite);
        }

        public static bool TryGetTowerButton(int towerId, bool canClick, out UnityEngine.Sprite sprite)
        {
            return BattleViewSpritePreloader.TryGetSprite(
                TowerAtlasBundle(towerId),
                canClick ? TowerCanClick1 : TowerCanClick0,
                out sprite);
        }

        public static bool TryGetMapBg(int bigLevel, int level, out UnityEngine.Sprite sprite)
        {
            string assetName = MapBgAssetName(bigLevel, level);
            return BattleViewSpritePreloader.TryGetSprite(RawImageBundle(assetName), assetName, out sprite);
        }

        public static bool TryGetMapRoad(int bigLevel, int level, out UnityEngine.Sprite sprite)
        {
            string assetName = MapRoadAssetName(bigLevel, level);
            return BattleViewSpritePreloader.TryGetSprite(RawImageBundle(assetName), assetName, out sprite);
        }

        public static bool TryGetMonsterPortrait(int monsterId, out UnityEngine.Sprite sprite)
        {
            string assetName = MonsterPortraitAssetName(monsterId);
            return BattleViewSpritePreloader.TryGetSprite(RawImageBundle(assetName), assetName, out sprite);
        }
    }
}
