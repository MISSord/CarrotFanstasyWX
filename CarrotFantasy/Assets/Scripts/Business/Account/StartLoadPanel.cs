namespace CarrotFantasy
{
    public class StartLoadPanel : BaseView
    {
        private static StartLoadPanel _instance;
        public static StartLoadPanel Instance => _instance ?? (_instance = new StartLoadPanel());

        private StartLoadPanel() { }

        public override void InitData()
        {
            viewName = "StartLoadPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.LoginPrefab, "StartLoadPanel");
        }
    }
}
