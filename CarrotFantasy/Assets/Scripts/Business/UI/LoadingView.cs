using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarrotFantasy
{
    public class LoadingView : BaseView
    {
        public override void InitData()
        {
            viewName = "LoadingView";
            // 切场景期间要盖住正在打开的新场景 UI，所以放高一层；场景就绪后立即关闭。
            layer = UILayer.Hight;
            SetUILoadInfo(0, UiViewAbPaths.LoadingViewPrefab, "LoadingPanel");
        }
    }
}
