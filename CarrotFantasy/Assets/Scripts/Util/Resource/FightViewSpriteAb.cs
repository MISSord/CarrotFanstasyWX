namespace CarrotFantasy
{
    /// <summary>
    /// 战斗视图 Sprite 的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致）。
    /// </summary>
    public static class FightViewSpriteAb
    {
        public const string NormalMordelAtlas = "ui/view/normalmordel/images_atlas";
        public const string CarrotAtlas = "fightpart/altassprite/carrot/images_atlas";
        /// <summary>全部战斗特效图在 fightpart/altassprite/effect/images_atlas。</summary>
        public const string EffectAtlasPrefix = "fightpart/altassprite/effect/";

        public const string GridNormal = "grid";
        public const string GridStart = "startsprite";
        public const string GridCantBuild = "cantbuild";

        public const string BtnCantUpLevel = "btn_cantuplevel";
        public const string BtnCanUpLevel = "btn_canuplevel";
        public const string BtnReachHighestLevel = "btn_reachhighestlevel";

        public const string PausePlaying = "pause_1";
        public const string PausePaused = "pause_3";

        /// <summary>
        /// 建造按钮精灵：<c>canclicktower_{塔id}_{0不可建|1可建}</c>，
        /// 放在 <see cref="NormalMordelAtlas"/>。
        /// </summary>
        public static string TowerCanClickAsset(int towerId, bool canClick)
        {
            return string.Format("canclicktower_{0}_{1}", towerId, canClick ? 1 : 0);
        }

        /// <summary>一座塔的身、攻击帧和子弹帧：FightPart/AltasSprite/Tower/{towerId}/images_atlas。</summary>
        public static string TowerAtlasBundle(int towerId)
        {
            return string.Format("fightpart/altassprite/tower/{0}/images_atlas", towerId);
        }

        /// <summary>道具图：FightPart/AltasSprite/Items/Level_{bigLevel}/images_atlas。</summary>
        public static string ItemAtlasBundle(int bigLevel)
        {
            return string.Format("fightpart/altassprite/items/level_{0}/images_atlas", bigLevel);
        }

        /// <summary>
        /// 同一大关的怪物控制器打在一个包里。资源名仍是 Monster_{关卡}_{id}。
        /// </summary>
        public static string MonsterAnimatorBundle(int bigLevel, int monsterId)
        {
            return string.Format("fightpart/fightprefab/monster/level_{0}_prefab", bigLevel);
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

        /// <summary>
        /// 小地图底图和路径：FightPart/Sprites/GameMap/Level_{bigLevel}/ 整层一个包。
        /// </summary>
        public static string MapSpriteBundle(int bigLevel)
        {
            return string.Format("fightpart/sprites/gamemap/level_{0}_prefab", bigLevel);
        }

        /// <summary>怪物帧图资源名：{monsterId}-1，例如 1-1。图在对应怪物图集里。</summary>
        public static string MonsterPortraitAssetName(int bigLevel, int monsterId)
        {
            return MonsterFrameAssetName(monsterId, 1);
        }

        /// <summary>怪物走路帧资源名：{monsterId}-{frame}，从 1 开始，例如 1-1、1-2。</summary>
        public static string MonsterFrameAssetName(int monsterId, int frame)
        {
            return string.Format("{0}-{1}", monsterId, frame);
        }

        /// <summary>
        /// 同一大关的怪物帧图打在一张图集里。
        /// </summary>
        public static string MonsterPortraitBundle(int bigLevel, int monsterId)
        {
            return string.Format("fightpart/altassprite/monster/level_{0}/images_atlas", bigLevel);
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
                NormalMordelAtlas,
                TowerCanClickAsset(towerId, canClick),
                out sprite);
        }
    }
}
