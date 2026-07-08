using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗入口线性流程诊断；Console 过滤关键字 <c>[BattleFlow]</c>。</summary>
    /// <remarks>
    /// 典型顺序：BeginSession → ExecutePipeline → 1/4 InitializingModel → 2/4 预加载 → 3/4 BuildingView → 4/4 Running。
    /// </remarks>
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
    }
}
