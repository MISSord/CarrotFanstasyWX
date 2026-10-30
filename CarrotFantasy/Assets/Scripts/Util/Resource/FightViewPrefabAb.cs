namespace CarrotFantasy
{
    /// <summary>
    /// 战斗相关预制体的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致；bundle 为小写清单名）。
    /// </summary>
    public static class FightViewPrefabAb
    {
        public const string FightPartBundle = "fightpart_prefab";
        public const string FightPartItemBundle = "fightpart/fightprefab/item_prefab";
        /// <summary>
        /// TowerList、BtnTowerBuild、HandleTowerCanvas、FightView 已放到 UI/View/NormalMordel，
        /// 与菜单、结算同一个包。
        /// </summary>
        public const string FightViewBundle = UiViewAbPaths.NormalMordelPrefab;
        public const string CarrotBundle = "fightpart/fightprefab/carrot_prefab";
        public const string BuildEffectBundle = "fightpart/fightprefab/buildeffect_prefab";
        public const string DestoryEffectBundle = "fightpart/fightprefab/destoryeffect_prefab";

        public static string TowerAssetName(int towerId, int levelIndex) => $"tower_{towerId}_{levelIndex}";

        /// <summary>
        /// Assets/Game/FightPart/FightPrefab/Tower/Tower_{id}_{level}/
        /// → fightpart/fightprefab/tower/tower_{id}_{level}_prefab
        /// </summary>
        public static string TowerBundle(int towerId, int levelIndex) =>
            $"fightpart/fightprefab/tower/tower_{towerId}_{levelIndex}_prefab";

        public static string ItemAssetName(int bigLevel, int itemId) => $"item_{bigLevel}_{itemId}";

        public static string BulletAssetName(int towerId, int levelIndex) => $"bullet_{towerId}_{levelIndex}";

        /// <summary>
        /// Assets/Game/FightPart/FightPrefab/Bullet/Bullet_{id}_{level}/
        /// → fightpart/fightprefab/bullet/bullet_{id}_{level}_prefab
        /// </summary>
        public static string BulletBundle(int towerId, int levelIndex) =>
            $"fightpart/fightprefab/bullet/bullet_{towerId}_{levelIndex}_prefab";

        public static string EffectAssetName(int towerId) => $"bullecteffect{towerId}";

        /// <summary>
        /// Assets/Game/FightPart/FightPrefab/Effect/BullectEffect{id}/
        /// → fightpart/fightprefab/effect/bullecteffect{id}_prefab
        /// </summary>
        public static string EffectBundle(int towerId) =>
            $"fightpart/fightprefab/effect/bullecteffect{towerId}_prefab";

        public const string BuildEffect = "buildeffect";
        public const string DestoryEffect = "destoryeffect";
        public const string Grid = "grid";
        public const string TowerList = "TowerList";
        public const string BtnTowerBuild = "BtnTowerBuild";
        public const string HandleTowerCanvas = "HandleTowerCanvas";
        // 资源名必须与预制体文件名一字不差（含大小写）：
        // Editor 下经 AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName 查找，
        // 名称对不上会落到 EditorAssetLoader 的"任意同类型"回退，可能返回子节点而非根节点。
        public const string NodeMap = "nodemap";
        public const string NodeTargetSignal = "nodetargetsign";
        public const string StartPoint = "startpoint";
        public const string Carrot = "carrot";
        public const string MonsterPrefab = "monsterprefab";
        public const string HpSlider = "hpslider";
        public const string DamageFloatText = "damagefloattext";
    }
}
