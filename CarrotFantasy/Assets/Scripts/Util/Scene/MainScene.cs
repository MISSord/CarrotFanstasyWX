using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class MainScene : BaseScene
    {
        public MainScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void InitSceneObject()
        {
            this.gameObj = GameObject.Find("Global");
        }

        public override void Dispose()
        {
            this.gameObj = null;
            base.Dispose();
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
