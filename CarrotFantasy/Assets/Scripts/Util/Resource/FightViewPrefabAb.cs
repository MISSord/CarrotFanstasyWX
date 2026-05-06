namespace CarrotFantasy
{
    /// <summary>
    /// 战斗相关预制体的 AB 包名与资源名（与 PrefabABPathPostprocessor 产物一致；bundle 为小写清单名）。
    /// </summary>
    public static class FightViewPrefabAb
    {
        public const string FightPartBundle = "fightpart_prefab";
        public const string FightViewBundle = "ui/view/fightview_prefab";

        public static string TowerSetBundleName(int towerId) => $"ui/view/fightview/tower/id{towerId}/towerset_prefab";

        public static string TowerBulletBundleName(int towerId) => $"ui/view/fightview/tower/id{towerId}/bullect_prefab";

        public static string ItemBundleName(int bigLevel) => $"ui/view/fightview/item/{bigLevel}_prefab";

        public const string BuildEffect = "BuildEffect";
        public const string DestoryEffect = "DestoryEffect";
        public const string Grid = "Grid";
        public const string TowerList = "tower_list";
        public const string BtnTowerBuild = "btn_tower_build";
        public const string HandleTowerCanvas = "handle_tower_canvas";
        public const string NodeMap = "nodeMap";
        public const string NodeTargetSignal = "node_target_signal";
        public const string StartPoint = "startPoint";
        public const string Carrot = "Carrot";
        public const string MonsterPrefab = "MonsterPrefab";
    }
}
