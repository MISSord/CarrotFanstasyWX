namespace CarrotFantasy
{
    //一开始进游戏的loading界面
    public class StartLoadPanel : BaseView
    {
        public override void InitData()
        {
            viewName = "StartLoadPanel";
            layer = UILayer.Normal;
            SetUIResourcesLoadInfo(0, UiViewAbPaths.StartLoadPanelResource, UiViewAbPaths.StartLoadPanelAsset);
        }
    }
}
