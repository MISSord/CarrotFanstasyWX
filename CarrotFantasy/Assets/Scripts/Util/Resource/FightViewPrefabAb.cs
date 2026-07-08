namespace CarrotFantasy
{
    /// <summary>
    /// 战斗相关预制体的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致；bundle 为小写清单名）。
    /// </summary>
    public static class FightViewPrefabAb
    {
        public const string FightPartBundle = "fightpart_prefab";
        public const string FightPartTowerBundle = "fightpart/tower_prefab";
        public const string FightPartItemBundle = "fightpart/item_prefab";
        public const string FightPartBulletBundle = "fightpart/bullet_prefab";
        public const string FightPartEffectBundle = "fightpart/effect_prefab";
        public const string FightViewBundle = "ui/view/fightview_prefab";

        public static string TowerAssetName(int towerId, int levelIndex) => $"Tower_{towerId}_{levelIndex}";

        public static string ItemAssetName(int bigLevel, int itemId) => $"Item_{bigLevel}_{itemId}";

        public static string BulletAssetName(int towerId, int levelIndex) => $"Bullet_{towerId}_{levelIndex}";

        public static string EffectAssetName(int towerId, int levelIndex) => $"Effect_{towerId}_{levelIndex}";

        public const string BuildEffect = "BuildEffect";
        public const string DestoryEffect = "DestoryEffect";
        public const string Grid = "Grid";
        public const string TowerList = "TowerList";
        public const string BtnTowerBuild = "BtnTowerBuild";
        public const string HandleTowerCanvas = "HandleTowerCanvas";
        public const string NodeMap = "NodeMap";
        public const string NodeTargetSignal = "NodeTargetSign";
        public const string StartPoint = "StartPoint";
        public const string Carrot = "Carrot";
        public const string MonsterPrefab = "MonsterPrefab";
        public const string HpSlider = "HPSlider";
        public const string DamageFloatText = "DamageFloatText";
    }
}
