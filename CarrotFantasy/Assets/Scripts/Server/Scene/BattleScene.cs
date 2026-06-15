using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗逻辑场景壳（流程第 1 步，Unity 场景就绪后执行）。
    /// InitSceneObject：解析 BattleRoot / ViewHost → Init：读取开战参数并交给 <see cref="BattleSessionHost.BeginSession"/>。
    /// </summary>
    public class BattleScene : BaseScene
    {
        BattleSceneContext sceneContext;
        bool listenerAdded;

        public BattleScene(BaseSceneType type, string name, Dictionary<string, dynamic> param) : base(type, name, param)
        {
            this.prefabUrl = null;
        }

        /// <summary>绑定 BattleScene.unity 中预置的 BattleRoot、ViewHost、SceneContainer。</summary>
        public override void InitSceneObject()
        {
            this.gameObj = BattleScenePresentation.ResolveBattleRootForSceneEntry();
            if (this.gameObj == null)
            {
                BattleFlowLog.Abort("InitSceneObject", "无法解析 BattleRoot");
                return;
            }

            BattleScenePresentation.EnsureBattleRootInActiveScene(this.gameObj);

            BattleSceneAnchor anchor = BattleSceneAnchor.FindOnBattleRoot(this.gameObj);
            if (anchor == null)
            {
                BattleFlowLog.Abort(
                    "InitSceneObject",
                    "BattleRoot#" + this.gameObj.GetInstanceID() +
                    " 缺少 BattleSceneAnchor，请在 BattleScene.unity 预先挂载");
                return;
            }

            BattleViewHost viewHost = anchor.ViewHost;
            if (viewHost == null)
            {
                BattleFlowLog.Abort(
                    "InitSceneObject",
                    "BattleRoot#" + this.gameObj.GetInstanceID() +
                    " 缺少 BattleViewHost，请在 BattleScene.unity 预先挂载");
                return;
            }

            GameObject sceneContainer = BattleScenePresentation.ResolveSceneContainerUnderBattleRoot(this.gameObj);
            if (sceneContainer == null)
            {
                BattleFlowLog.Abort("InitSceneObject", "无法解析 SceneContainer");
                return;
            }

            viewHost.BindSceneContainer(sceneContainer);
            // 场景引用只存于 Context，开战参数经 BeginSession 注入 BaseBattle.LaunchParams
            this.sceneContext = anchor.CreateContext();
        }

        /// <summary>场景壳就绪后启动单局 Session（Model 初始化 + 视图流水线）。</summary>
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

        /// <summary>校验 Context 与 BattleLauncher 写入的开战参数，创建并 Run Session。</summary>
        bool TryBeginSession()
        {
            if (this.sceneContext == null || !this.sceneContext.IsValid)
            {
                BattleFlowLog.Abort("Init", "BattleSceneContext 无效，InitSceneObject 可能失败，跳过 BeginSession");
                return false;
            }

            if (!this.sceneContext.IsSceneAlive())
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

            ServerProvision.battleSessionHost.BeginSession(launchParams, this.sceneContext);
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

            // 场景卸载会销毁 BattleRoot 子树，此处只清逻辑层，避免与 Unity 卸载竞态 Destroy
            ServerProvision.battleSessionHost?.EndSession(
                clearLaunchParams: true,
                destroyViewHierarchy: false);

            if (this.listenerAdded)
            {
                this.RemoveListener();
                this.listenerAdded = false;
            }

            this.sceneContext = null;
            this.gameObj = null;
        }
    }
}
