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
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.LoadingViewPrefab, "LoadingPanel");
        }
    }
}
