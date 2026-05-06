namespace CarrotFantasy
{
    /// <summary>
    /// AB 名与 PrefabABPathPostprocessor 规则一致：Assets/Game/ 后目录转小写 + 后缀，资源名为 prefab 短名（无扩展名）。
    /// </summary>
    public static class UiViewAbPaths
    {
        public const string SettingViewPrefab = "ui/view/settingview_prefab";
        public const string MainViewViewPrefab = "ui/view/mainview_prefab";
        public const string HelpPrefab = "ui/view/help_prefab";
        public const string MapViewPrefab = "ui/view/mapview_prefab";
        public const string RoomPrefab = "ui/view/room_prefab";
        public const string LoginPrefab = "ui/view/login_prefab";
        public const string ViewRootPrefab = "ui/view_prefab";
        public const string LoadingViewPrefab = "ui/view/loadingview_prefab";
        public const string NormalMordelPrefab = "ui/view/normalmordel_prefab";

        /// <summary>入口 Loading 面板所在 AB（需在编辑器中为对应预制体指定该 bundle）。</summary>
        public const string StartLoadPanelBundle = "ui/view/startload_prefab";

        public const string StartLoadPanelAsset = "StartLoadPanel";

        public const string MapNodeLevelAsset = "node_level";
        public const string MapNodeTowerAsset = "node_tower";
    }
}
