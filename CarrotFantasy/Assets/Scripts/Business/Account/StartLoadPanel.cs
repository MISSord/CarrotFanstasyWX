using UnityEngine.UI;

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

        /// <summary>
        /// 启动画面只是背景，关掉射线检测，避免提前打开时挡住 AotBootPanel 的下载/退出按钮。
        /// </summary>
        protected override void LoadCallBack()
        {
            if (transform == null)
            {
                return;
            }

            RawImage background = transform.GetComponent<RawImage>();
            if (background != null)
            {
                background.raycastTarget = false;
            }
        }
    }
}
