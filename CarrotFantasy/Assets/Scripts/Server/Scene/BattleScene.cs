using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗逻辑场景壳：解析 BattleViewHost → BeginSession。
    /// </summary>
    public class BattleScene : BaseScene
    {
        BattleViewHost viewHost;
        bool listenerAdded;

        public BattleScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        public override void InitSceneObject()
        {
            this.viewHost = BattleViewHost.FindInLoadedBattleScene();
            if (this.viewHost == null || !this.viewHost.EnsureReady())
            {
                BattleFlowLog.Abort("InitSceneObject", "BattleViewHost 未就绪");
                return;
            }

            this.gameObj = this.viewHost.gameObject;

            Scene rootScene = this.gameObj.scene;
            Scene activeScene = SceneManager.GetActiveScene();
            if (rootScene.IsValid() && activeScene != rootScene)
            {
                SceneManager.SetActiveScene(rootScene);
            }
        }

        public override void Init()
        {
            base.Init();

            if (!this.TryBeginSession())
            {
                return;
            }

            this.AddListener();
            this.listenerAdded = true;
        }

        bool TryBeginSession()
        {
            if (this.viewHost == null || !this.viewHost.IsReady)
            {
                BattleFlowLog.Abort("Init", "BattleViewHost 无效，InitSceneObject 可能失败，跳过 BeginSession");
                return false;
            }

            if (!this.viewHost.IsSceneAlive())
            {
                BattleFlowLog.Abort("Init", "BattleScene Unity 场景壳未就绪");
                return false;
            }

            PveModelBattleParams launchParams = BattleParamServer.Instance?.CurrentPveParams;
            if (launchParams == null)
            {
                BattleFlowLog.Abort("Init", "CurrentPveParams 为空，请从 BattleLauncher 进关");
                return false;
            }

            ServerProvision.battleSessionHost.BeginSession(launchParams, this.viewHost);
            return true;
        }

        private void AddListener()
        {
            BusinessProvision.Instance.eventDispatcher.AddListener(
                CommonEventType.RETURN_TO_MAIN_SCENE,
                this.ReturnToMainScene);
        }

        private void RemoveListener()
        {
            BusinessProvision.Instance.eventDispatcher.RemoveListener(
                CommonEventType.RETURN_TO_MAIN_SCENE,
                this.ReturnToMainScene);
        }

        private void ReturnToMainScene()
        {
            ServerProvision.sceneServer.LoadScene(BaseSceneType.MainScene, null);
        }

        public override void Dispose()
        {
            int rootId = this.gameObj != null ? this.gameObj.GetInstanceID() : 0;
            Debug.LogWarning("[BattleScene] Dispose: BattleRoot#" + rootId);

            ServerProvision.battleSessionHost?.EndSession(
                clearLaunchParams: true,
                destroyViewHierarchy: false);

            if (this.listenerAdded)
            {
                this.RemoveListener();
                this.listenerAdded = false;
            }

            this.viewHost = null;
            this.gameObj = null;
        }
    }
}
