using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗入口线性流程诊断；Console 过滤关键字 <c>[BattleFlow]</c>。</summary>
    public static class BattleFlowLog
    {
        public const string Tag = "[BattleFlow]";

        public static void Step(string step, string detail = null)
        {
            if (string.IsNullOrEmpty(detail))
            {
                Debug.Log(Tag + " " + step);
                return;
            }

            Debug.Log(Tag + " " + step + " | " + detail);
        }

        public static void Abort(string step, string reason)
        {
            Debug.LogError(Tag + " 中止@" + step + " | " + reason);
        }

        public static void ViewHostSnapshot(string step, BattleViewHost viewHost)
        {
            if (viewHost == null)
            {
                Abort(step, "ViewHost=null");
                return;
            }

            GameObject sceneContainer = viewHost.SceneContainer;
            int sceneChildCount = viewHost.GetSceneContainerChildCount();
            int gridChildCount = viewHost.GetContainerChildCount("GridContainer");
            string sceneContainerId = sceneContainer != null
                ? sceneContainer.GetInstanceID().ToString()
                : "null";

            Step(
                step,
                "ViewHost#" + viewHost.GetInstanceID() +
                " BattleRoot#" + viewHost.gameObject.GetInstanceID() +
                " SceneContainer#" + sceneContainerId +
                " sceneChildren=" + sceneChildCount +
                " gridChildren=" + gridChildCount);
        }
    }
}
