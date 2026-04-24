using System.Collections.Generic;

namespace CarrotFantasy
{
    public class MainScene : BaseScene
    {

        public MainScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void init()
        {
            base.init();
            StartLoadPanel panel = new StartLoadPanel(null);
            //ServerProvision.panelServer.ShowPanel(panel);
            //Sche.delayExeOnceTimes(() => {
            //    panel.autoClose();
            //    ServerProvision.panelServer.ShowPanel(new MainPanel(null));
            //    UIServer.Instance.fadeLoadingPanel();
            //    if (AccountServer.Instance.userId == 0)
            //    {
            //        ServerProvision.panelServer.ShowPanel(new LoginPanel(null));
            //    }
            //    if(MapServer.Instance.curBigLevel != 0)
            //    {
            //        ServerProvision.panelServer.ShowPanel(new MapBigLevelPanel(null));
            //        MapNormalLevelPanel panelOne = new MapNormalLevelPanel(null);
            //        panelOne.currentBigLevelID = MapServer.Instance.curBigLevel;
            //        ServerProvision.panelServer.ShowPanel(panelOne);
            //    }
            //}, 2f);

        }
    }
}
