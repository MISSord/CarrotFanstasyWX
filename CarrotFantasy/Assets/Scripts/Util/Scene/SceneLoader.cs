using System;
using System.Collections;
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

        /// <summary>
        /// 同步加载指定场景。
        /// </summary>
        public static void Load(GameSceneType sceneType, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            string sceneName = ToSceneName(sceneType);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[SceneLoader] 未找到场景映射: {sceneType}");
                return;
            }

            SceneManager.LoadScene(sceneName, loadMode);
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
                default:
                    return string.Empty;
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
