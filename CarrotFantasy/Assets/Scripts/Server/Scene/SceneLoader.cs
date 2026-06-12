using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    /// <summary>
    /// Unity 原生场景加载与切换工具类。
    /// 支持同步切换、异步切换和重载当前场景。
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        private static SceneLoader _runner;

        private static SceneLoader Runner
        {
            get
            {
                if (_runner != null)
                {
                    return _runner;
                }

                GameObject go = new GameObject("[SceneLoader]");
                DontDestroyOnLoad(go);
                _runner = go.AddComponent<SceneLoader>();
                return _runner;
            }
        }

        public static SceneLoader RunnerInstance
        {
            get { return Runner; }
        }

        public static Coroutine StartRoutine(IEnumerator routine)
        {
            return Runner.StartCoroutine(routine);
        }

        /// <summary>
        /// 同步加载指定场景。
        /// </summary>
        public static bool TryLoad(GameSceneType sceneType, LoadSceneMode loadMode, out string error)
        {
            error = null;
            string scenePath = ToScenePath(sceneType);
            string sceneName = ToSceneName(sceneType);
            if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(sceneName))
            {
                error = "未找到场景映射: " + sceneType;
                return false;
            }

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex < 0)
            {
                error = scenePath + " 未加入 Build Settings";
                return false;
            }

            SceneManager.LoadScene(buildIndex, loadMode);

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || active.name != sceneName)
            {
                error = "LoadScene 后 active=" + active.name +
                        " 期望=" + sceneName +
                        " loaded=[" + BuildLoadedSceneNameList() + "]";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 同步加载指定场景（失败时仅打 LogError，保持旧调用方兼容）。
        /// </summary>
        public static void Load(GameSceneType sceneType, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (!TryLoad(sceneType, loadMode, out string error))
            {
                Debug.LogError("[SceneLoader] " + error);
            }
        }

        /// <summary>
        /// 异步加载指定场景；完成回调返回是否加载成功且 active 场景名匹配。
        /// </summary>
        public static void TryLoadAsync(
            GameSceneType sceneType,
            LoadSceneMode loadMode,
            Action<bool> onCompleted,
            Action<float> onProgress = null)
        {
            Runner.StartCoroutine(TryLoadAsyncCoroutine(sceneType, loadMode, onCompleted, onProgress));
        }

        static IEnumerator TryLoadAsyncCoroutine(
            GameSceneType sceneType,
            LoadSceneMode loadMode,
            Action<bool> onCompleted,
            Action<float> onProgress)
        {
            string scenePath = ToScenePath(sceneType);
            string sceneName = ToSceneName(sceneType);
            if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] 未找到场景映射: " + sceneType);
                onCompleted?.Invoke(false);
                yield break;
            }

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (buildIndex < 0)
            {
                Debug.LogError("[SceneLoader] " + scenePath + " 未加入 Build Settings");
                onCompleted?.Invoke(false);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, loadMode);
            if (operation == null)
            {
                Debug.LogError("[SceneLoader] 场景异步加载失败: " + sceneName);
                onCompleted?.Invoke(false);
                yield break;
            }

            while (!operation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(operation.progress / 0.9f);
                onProgress?.Invoke(normalizedProgress);
                yield return null;
            }

            onProgress?.Invoke(1f);

            Scene active = SceneManager.GetActiveScene();
            bool ok = active.IsValid() && active.name == sceneName;
            if (!ok)
            {
                Debug.LogError(
                    "[SceneLoader] LoadSceneAsync 后 active=" + active.name +
                    " 期望=" + sceneName +
                    " loaded=[" + BuildLoadedSceneNameList() + "]");
            }

            onCompleted?.Invoke(ok);
        }

        /// <summary>
        /// 异步加载指定场景，可监听进度与完成回调。
        /// </summary>
        public static void LoadAsync(
            GameSceneType sceneType,
            Action<float> onProgress = null,
            Action onCompleted = null,
            LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            string sceneName = ToSceneName(sceneType);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[SceneLoader] 未找到场景映射: {sceneType}");
                return;
            }

            Runner.StartCoroutine(Runner.LoadSceneCoroutine(sceneName, loadMode, onProgress, onCompleted));
        }

        /// <summary>
        /// 重新加载当前激活场景。
        /// </summary>
        public static void ReloadCurrent(LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            Scene current = SceneManager.GetActiveScene();
            if (!current.IsValid() || string.IsNullOrEmpty(current.name))
            {
                Debug.LogError("[SceneLoader] 当前场景无效，无法重载。");
                return;
            }

            SceneManager.LoadScene(current.name, loadMode);
        }

        /// <summary>
        /// 在已加载场景列表中按名称查找（比 GetSceneByName 更可靠，兼容编辑器直接 Play 的场景）。
        /// </summary>
        public static Scene FindLoadedSceneByName(string unitySceneName)
        {
            if (string.IsNullOrEmpty(unitySceneName))
            {
                return default;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == unitySceneName)
            {
                return active;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.name == unitySceneName)
                {
                    return scene;
                }
            }

            Scene byName = SceneManager.GetSceneByName(unitySceneName);
            if (byName.IsValid())
            {
                return byName;
            }

            return default;
        }

        public static string BuildLoadedSceneNameList()
        {
            var sb = new StringBuilder(64);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(scene.name);
                sb.Append(scene.isLoaded ? "(loaded)" : "(not loaded)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 把枚举转换成 Unity 场景资源路径。
        /// </summary>
        public static string ToScenePath(GameSceneType sceneType)
        {
            switch (sceneType)
            {
                case GameSceneType.MainScene:
                    return "Assets/Game/Scenes/MainScene.unity";
                case GameSceneType.BattleScene:
                    return "Assets/Game/Scenes/BattleScene.unity";
                case GameSceneType.RoguelikeMapScene:
                    return "Assets/Game/Scenes/Scene.unity";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 把枚举转换成 Unity 场景名。
        /// </summary>
        public static string ToSceneName(GameSceneType sceneType)
        {
            switch (sceneType)
            {
                case GameSceneType.MainScene:
                    return "MainScene";
                case GameSceneType.BattleScene:
                    return "BattleScene";
                case GameSceneType.RoguelikeMapScene:
                    return "Scene";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 逻辑场景类型与 <see cref="GameSceneType"/> 的对应关系（无独立 Unity 场景时返回 <see cref="GameSceneType.None"/>）。
        /// </summary>
        public static GameSceneType ToGameSceneType(BaseSceneType baseSceneType)
        {
            switch (baseSceneType)
            {
                case BaseSceneType.MainScene:
                    return GameSceneType.MainScene;
                case BaseSceneType.BattleScene:
                    return GameSceneType.BattleScene;
                case BaseSceneType.RoguelikeMapScene:
                    return GameSceneType.RoguelikeMapScene;
                default:
                    return GameSceneType.None;
            }
        }

        private IEnumerator LoadSceneCoroutine(
            string sceneName,
            LoadSceneMode loadMode,
            Action<float> onProgress,
            Action onCompleted)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] 场景异步加载失败: {sceneName}");
                yield break;
            }

            while (!operation.isDone)
            {
                // Unity 异步加载在完成前 progress 通常最大为 0.9，这里归一化到 0-1。
                float normalizedProgress = Mathf.Clamp01(operation.progress / 0.9f);
                onProgress?.Invoke(normalizedProgress);
                yield return null;
            }

            onProgress?.Invoke(1f);
            onCompleted?.Invoke();
        }
    }
}
