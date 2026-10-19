using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 启动期资源更新的**入口与工具**。
    /// <para>
    /// 流程本身由 <see cref="UpdateFlow"/> 状态机驱动（状态在 <c>UpdateStates.cs</c>）。
    /// 这里只保留：
    /// 1. 创建状态机（<see cref="CreateFlow"/>）；
    /// 2. 远程不可用时的两级回退注入（缓存清单 / 包内基线清单）；
    /// 3. 结束进程与致命错误面板（面板是协程，供启动后半段复用）。
    /// </para>
    /// </summary>
    public static class AotResourceUpdateRunner
    {
        public enum Result
        {
            Success,
            Failed,
            Cancelled,
        }

        /// <summary>
        /// 创建并返回状态机实例（未启动）。调用方负责每帧 <see cref="UpdateFlow.Tick"/>。
        /// </summary>
        public static UpdateFlow CreateFlow(MonoBehaviour host)
        {
            UpdateContext ctx = new UpdateContext
            {
                // 状态只需要 UI 挂点；真要用 MonoBehaviour 时可由 Parent.gameObject 取。
                Parent = host != null ? host.transform : null,
            };

            return UpdateFlow.Create(ctx);
        }

        /// <summary>远程不可用且本地无清单时，用包里内置的基线清单兜底（断网首启）。</summary>
        internal static bool TryInjectBundledManifest()
        {
            string path = AssetBundlePathHelper.GetStreamingAssetsManifestPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                CustomManifest manifest = JsonUtility.FromJson<CustomManifest>(File.ReadAllText(path));
                if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
                {
                    return false;
                }

                // 内置清单存在但包体没拷进 StreamingAssets 时，注入也没用，直接放弃。
                bool anyAvailable = false;
                for (int i = 0; i < manifest.AssetBundles.Count && !anyAvailable; i++)
                {
                    CustomAssetBundleInfo info = manifest.AssetBundles[i];
                    anyAvailable = info != null && AssetBundlePathHelper.IsBundleAvailableAtRuntime(info.BundleName);
                }

                if (!anyAvailable)
                {
                    Debug.LogWarning("[AotResourceUpdateRunner] 内置清单存在，但 StreamingAssets 中没有可用 AB，忽略");
                    return false;
                }

                AssetBundleManager.Instance.SetAssetBundleItem(manifest);
                Debug.LogWarning($"[AotResourceUpdateRunner] 已注入 StreamingAssets 基线清单，共 {manifest.AssetBundles.Count} 个 AB");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AotResourceUpdateRunner] 读取内置清单失败: " + e.Message);
                return false;
            }
        }

        /// <summary>远程失败时尝试使用 persistentDataPath 中上次成功保存的清单。</summary>
        internal static bool TryInjectCachedLocalManifest()
        {
            string path = AssetBundlePathHelper.GetPersistentManifestPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                CustomManifest manifest = JsonUtility.FromJson<CustomManifest>(json);
                if (manifest?.AssetBundles == null || manifest.AssetBundles.Count == 0)
                {
                    return false;
                }

                AssetBundleManager.Instance.SetAssetBundleItem(manifest);
                Debug.Log(
                    $"[AotResourceUpdateRunner] 已注入本地清单，共 {manifest.AssetBundles.Count} 个 AB");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AotResourceUpdateRunner] 读取本地清单失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 致命错误面板：提示后退出。等玩家点按，属于"阻塞式"协程，
        /// 供启动后半段（Lua / 业务入口）复用；更新流程内部的面板由状态机任务实现。
        /// </summary>
        public static IEnumerator ShowFatalErrorDialog(MonoBehaviour host, string message)
        {
            AotBootUi.EnsureEventSystem();

            bool exitChosen = false;
            Transform parent = host != null ? host.transform : null;

            var dialog = new AotUpdateListErrorView();

            // onRestart 传 null：没有可靠的重启实现时，按钮会被隐藏而不是点了没反应。
            if (!dialog.Open(message, () => exitChosen = true, null, parent))
            {
                // 连面板都开不出来时，至少留下日志并退出，避免静默卡住。
                Debug.LogError("[AotResourceUpdateRunner] 无法显示错误面板: " + message);
                QuitApp();
                yield break;
            }

            while (!exitChosen)
            {
                yield return null;
            }

            QuitApp();
        }

        internal static void QuitApp()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
