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
            this.RestoreMainSceneUi();
        }

        /// <summary>
        /// 战斗返回主场景时恢复选关界面；首次进游戏（无进关记录）由 EnterGameState 打开 MainPanel。
        /// </summary>
        void RestoreMainSceneUi()
        {
            MapServer mapServer = MapServer.Instance;
            if (mapServer == null || mapServer.LastEnteredBigLevelId <= 0)
            {
                return;
            }

            if (ViewManager.Instance == null)
            {
                BattleFlowLog.Abort("RestoreMainSceneUi", "ViewManager=null");
                return;
            }

            ViewManager.Instance.OpenView<MapBigLevelPanel>();

            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(MapNormalLevelPanel), out BaseView levelView))
            {
                BattleFlowLog.Abort("RestoreMainSceneUi", "MapNormalLevelPanel 未注册");
                return;
            }

            var levelPanel = (MapNormalLevelPanel)levelView;
            levelPanel.OpenForBigLevel(mapServer.LastEnteredBigLevelId, mapServer.LastEnteredLevelId);
            ViewManager.Instance.OpenView<MapNormalLevelPanel>();
        }
    }
}
