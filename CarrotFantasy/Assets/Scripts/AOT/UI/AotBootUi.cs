using UnityEngine;
using UnityEngine.EventSystems;

namespace CarrotFantasy
{
    /// <summary>AOT 启动 UI 公共辅助。</summary>
    public static class AotBootUi
    {
        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("AotEventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
