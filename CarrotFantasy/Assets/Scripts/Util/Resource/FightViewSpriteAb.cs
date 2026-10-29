namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图 Sprite 的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致）。
    /// </summary>
    public static class FightViewSpriteAb
    {
        public const string NormalMordelAtlas = "ui/view/normalmordel/images_atlas";
        public const string CarrotAtlas = "ui/images/carrot/images_atlas";

        public const string GridNormal = "grid";
        public const string GridStart = "startsprite";
        public const string GridCantBuild = "cantbuild";

        public const string BtnCantUpLevel = "btn_cantuplevel";
        public const string BtnCanUpLevel = "btn_canuplevel";
        public const string BtnReachHighestLevel = "btn_reachhighestlevel";

        public const string PausePlaying = "pause_1";
        public const string PausePaused = "pause_3";

        public const string TowerCanClick0 = "canclick0";
        public const string TowerCanClick1 = "canclick1";

        public static string TowerAtlasBundle(int towerId)
        {
            return string.Format("ui/images/tower/{0}/images_atlas", towerId);
        }

        /// <summary>
        /// 每个怪物独立文件夹后，包名与 PrefabABPathPostprocessor 一致：
        /// Assets/Game/FightPart/Monster/Monster_{关卡}_{id}/ → fightpart/monster/monster_{关卡}_{id}_prefab
        /// </summary>
        public static string MonsterAnimatorBundle(int bigLevel, int monsterId)
        {
            return string.Format("fightpart/monster/monster_{0}_{1}_prefab", bigLevel, monsterId);
        }

        /// <summary>控制器资源名与文件夹名一致：Monster_{关卡}_{id}。</summary>
        public static string MonsterAnimatorAssetName(int bigLevel, int monsterId)
        {
            return string.Format("Monster_{0}_{1}", bigLevel, monsterId);
        }

        public static string CarrotStateAsset(int stateIndex)
        {
            return stateIndex.ToString();
        }

        /// <summary>关卡小地图底图：Level_{bigLevel}_{level}_BG（全局唯一，避免跨章节同名冲突）。</summary>
        public static string MapBgAssetName(int bigLevel, int level)
        {
            return string.Format("level_{0}_{1}_bg", bigLevel, level);
        }

        /// <summary>关卡小地图路径图：Level_{bigLevel}_{level}_Road。</summary>
        public static string MapRoadAssetName(int bigLevel, int level)
        {
            return string.Format("level_{0}_{1}_road", bigLevel, level);
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

        /// <summary>小地图 BG/Road 包：Assets/Game/UI/Sprites/GameMap/Level_{bigLevel}/。</summary>
        public static string MapSpriteBundle(int bigLevel, string assetName)
        {
            return string.Format("ui/sprites/gamemap/level_{0}/{1}", bigLevel, assetName.ToLowerInvariant());
        }

        /// <summary>怪物头像资源名：{monsterId}-1。大关区分放在 Level_{bigLevel} 目录里。</summary>
        public static string MonsterPortraitAssetName(int bigLevel, int monsterId)
        {
            return string.Format("{0}-1", monsterId);
        }

        /// <summary>怪物头像包：Assets/Game/UI/Sprites/Monster/Level_{bigLevel}/{asset}。</summary>
        public static string MonsterPortraitBundle(int bigLevel, string assetName)
        {
            return string.Format("ui/sprites/monster/level_{0}/{1}", bigLevel, assetName.ToLowerInvariant());
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
