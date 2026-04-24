using System.Collections.Generic;

namespace CarrotFantasy
{
    public class StartLoadPanel : BasePanel
    {
        public StartLoadPanel(Dictionary<string, dynamic> param) : base(param)
        {
            this.isClickGrayEnable = false;
            this.prefabUrl = "Prefabs/Business/Login/StartLoadPanel";
        }

        public override void Init()
        {
            base.Init();
        }

        public void autoClose()
        {
            this.Finish();
        }
    }
}
