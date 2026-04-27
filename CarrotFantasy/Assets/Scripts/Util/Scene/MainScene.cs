using System.Collections.Generic;

namespace CarrotFantasy
{
    public class MainScene : BaseScene
    {
        public MainScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void Init()
        {
            base.Init();
            // 示例：StartLoadPanel.Instance + UIViewService.OpenStartLoadPanel();
            //Sche.DelayExeOnceTimes(() => {
            //    panel.autoClose();
            //    UIViewService.OpenMainPanel();
            //    UIServer.Instance.FadeLoadingPanel();
            //    if (AccountServer.Instance.userId == 0)
            //    {
            //        UIViewService.OpenLoginPanel();
            //    }
            //    if(MapServer.Instance.curBigLevel != 0)
            //    {
            //        UIViewService.OpenMapBigLevelPanel();
            //        UIViewService.OpenMapNormalLevelPanel(MapServer.Instance.curBigLevel);
            //    }
            //}, 2f);

        }
    }
}
