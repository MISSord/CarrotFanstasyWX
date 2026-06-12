using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>BattleRoot 上的场景锚点：仅持有 ViewHost，不参与 Tick / Session 编排。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BattleViewHost))]
    public sealed class BattleSceneAnchor : MonoBehaviour
    {
        BattleViewHost viewHost;

        public BattleViewHost ViewHost
        {
            get
            {
                if (this.viewHost == null)
                {
                    this.viewHost = this.GetComponent<BattleViewHost>();
                }

                return this.viewHost;
            }
        }

        public static BattleSceneAnchor FindOnBattleRoot(GameObject battleRoot)
        {
            if (battleRoot == null)
            {
                return null;
            }

            return battleRoot.GetComponent<BattleSceneAnchor>();
        }

        public BattleSceneContext CreateContext()
        {
            BattleViewHost host = this.ViewHost;
            if (host == null)
            {
                return null;
            }

            return new BattleSceneContext(this.gameObject, host);
        }
    }
}
