using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    /// <summary>单次战斗的 Unity 场景壳引用；由 BattleScene 就绪后注入 Session，不写入 config。</summary>
    public sealed class BattleSceneContext
    {
        public GameObject BattleRoot { get; }
        public BattleViewHost ViewHost { get; }

        public bool IsValid
        {
            get { return this.BattleRoot != null && this.ViewHost != null; }
        }

        public BattleSceneContext(GameObject battleRoot, BattleViewHost viewHost)
        {
            this.BattleRoot = battleRoot;
            this.ViewHost = viewHost;
        }

        /// <summary>异步回调前确认 BattleRoot 仍归属已加载的 BattleScene。</summary>
        public bool IsSceneAlive()
        {
            if (!this.IsValid)
            {
                return false;
            }

            Scene scene = this.BattleRoot.scene;
            return scene.IsValid() &&
                   scene.isLoaded &&
                   scene.name == BattleScenePresentation.BattleUnitySceneName;
        }
    }
}
