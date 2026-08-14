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

        /// <summary>战斗 Sprite 包：ui/sprites/{relPath}（小地图/怪物头像等按 Sprite 导入）。</summary>
        public static string SpriteBundle(string relPath)
        {
            return string.Format("ui/sprites/{0}", relPath.ToLowerInvariant());
        }

        /// <summary>小地图 BG/Road 包（按 bigLevel 分目录）。</summary>
        public static string MapSpriteBundle(int bigLevel, string assetName)
        {
            return string.Format("ui/sprites/gamemap/{0}/{1}", bigLevel, assetName.ToLowerInvariant());
        }

        /// <summary>怪物头像资源名：{monsterId}-{idx}。带 bigLevel 区分不同大关同名怪物头像。</summary>
        public static string MonsterPortraitAssetName(int bigLevel, int monsterId)
        {
            return string.Format("{0}-1", monsterId);
        }

        /// <summary>怪物头像包：按 bigLevel 分层，ui/sprites/monster/{bigLevel}/{asset}。</summary>
        public static string MonsterPortraitBundle(int bigLevel, string assetName)
        {
            return string.Format("ui/sprites/monster/{0}/{1}", bigLevel, assetName.ToLowerInvariant());
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
    }
}
