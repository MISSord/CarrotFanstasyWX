using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    public class SceneServer
    {
        private EventDispatcher eventDispatcher;
        private Camera uiCamera;
        private Camera mainCamera;
        public BaseScene currentScene;

        bool isLoading;
        Coroutine loadRoutine;

        public bool IsLoading
        {
            get { return this.isLoading; }
        }

        public void Init()
        {
            this.currentScene = null;
            this.eventDispatcher = new EventDispatcher();
            this.BindUICameraFromActiveScene();
        }

        public Camera GetUICamera()
        {
            return this.uiCamera;
        }

        public EventDispatcher GetEventDispatcher()
        {
            return this.eventDispatcher;
        }

        public BaseScene GetCurScene()
        {
            return currentScene;
        }

        private void RemoveScene()
        {
            if (this.currentScene == null)
            {
                return;
            }

            ViewManager.Instance.CloseAllPanel(PanelCloseReasonType.SCENE_CHANGE, this.currentScene.sceneType);
            ViewManager.Instance.SetShowPanelActive(false);
            this.currentScene.Dispose();
            this.currentScene = null;
        }

        static bool ShouldSkipSameSceneLoad(BaseSceneType sceneType, BaseScene current)
        {
            if (current == null)
            {
                return false;
            }

            if (current.sceneType != sceneType)
            {
                return false;
            }

            // 战斗每次进关都走完整重载，不因逻辑层仍标记为 BattleScene 而跳过
            return sceneType != BaseSceneType.BattleScene;
        }

        /// <summary>异步切换逻辑场景；Unity 场景就绪后才 InitSceneObject / Init。</summary>
        public void LoadScene(BaseSceneType sceneType, Dictionary<String, dynamic> param, Action<bool> onComplete = null)
        {
            if (this.isLoading)
            {
                BattleFlowLog.Abort("LoadScene", "已有场景加载进行中，忽略 " + sceneType);
                onComplete?.Invoke(false);
                return;
            }

            if (ShouldSkipSameSceneLoad(sceneType, this.currentScene))
            {
                onComplete?.Invoke(false);
                return;
            }

            if (this.loadRoutine != null)
            {
                this.isLoading = false;
                SceneLoader.RunnerInstance.StopCoroutine(this.loadRoutine);
                this.loadRoutine = null;
            }

            this.isLoading = true;
            this.loadRoutine = SceneLoader.StartRoutine(this.LoadSceneRoutine(sceneType, param, onComplete));
        }

        /// <summary>兼容旧调用；返回 false 表示未启动加载。</summary>
        public bool LoadScene(BaseSceneType sceneType, Dictionary<String, dynamic> param)
        {
            if (this.isLoading)
            {
                return false;
            }

            if (ShouldSkipSameSceneLoad(sceneType, this.currentScene))
            {
                return false;
            }

            this.LoadScene(sceneType, param, null);
            return true;
        }

        IEnumerator LoadSceneRoutine(BaseSceneType sceneType, Dictionary<String, dynamic> param, Action<bool> onComplete)
        {
            try
            {
                if (this.currentScene != null)
                {
                    this.RemoveScene();
                }

                bool success = false;
                yield return this.LoadUnitySceneAndInitRoutine(sceneType, param, result => success = result);
                onComplete?.Invoke(success);
            }
            finally
            {
                this.isLoading = false;
                this.loadRoutine = null;
            }
        }

        IEnumerator LoadUnitySceneAndInitRoutine(
            BaseSceneType sceneType,
            Dictionary<String, dynamic> param,
            Action<bool> onComplete)
        {
            GameSceneType unitySceneType = SceneLoader.ToGameSceneType(sceneType);
            string unitySceneName = SceneLoader.ToSceneName(unitySceneType);

            if (!string.IsNullOrEmpty(unitySceneName))
            {
                Scene active = SceneManager.GetActiveScene();
                bool needUnityLoad = sceneType == BaseSceneType.BattleScene ||
                                     !active.IsValid() ||
                                     active.name != unitySceneName;

                // 战斗场景每次进关强制重载 Unity 场景（见 ShouldSkipSameSceneLoad）
                if (needUnityLoad)
                {
                    bool loadDone = false;
                    bool loadOk = false;
                    SceneLoader.TryLoadAsync(
                        unitySceneType,
                        LoadSceneMode.Single,
                        ok =>
                        {
                            loadOk = ok;
                            loadDone = true;
                        });

                    while (!loadDone)
                    {
                        yield return null;
                    }

                    if (!loadOk)
                    {
                        if (sceneType == BaseSceneType.BattleScene)
                        {
                            BattleFlowLog.Abort("LoadSceneProgress", "Unity 场景异步加载失败: " + unitySceneName);
                        }
                        else
                        {
                            Debug.LogError("[SceneServer] Unity 场景异步加载失败: " + unitySceneName);
                        }

                        onComplete?.Invoke(false);
                        yield break;
                    }
                }

                if (!this.TryEnsureUnitySceneActive(unitySceneName, out string sceneError))
                {
                    if (sceneType == BaseSceneType.BattleScene)
                    {
                        BattleFlowLog.Abort("LoadSceneProgress", sceneError);
                    }
                    else
                    {
                        Debug.LogError("[SceneServer] " + sceneError);
                    }

                    onComplete?.Invoke(false);
                    yield break;
                }

                if (sceneType == BaseSceneType.BattleScene)
                {
                    BattleFlowLog.Step(
                        "LoadSceneProgress",
                        "SceneReady active=" + SceneManager.GetActiveScene().name);
                }
            }

            this.BindUICameraFromActiveScene();
            ViewManager.Instance?.RebindScenePresentation();

            BaseScene targetScene = null;
            switch (sceneType)
            {
                case BaseSceneType.MainScene:
                    targetScene = new MainScene(sceneType, "MainScene", param);
                    break;
                case BaseSceneType.BattleScene:
                    // 逻辑场景壳；Unity 场景名 BattleScene 须已加载
                    targetScene = new BattleScene(sceneType, "BattleScene", param);
                    break;
                case BaseSceneType.RoguelikeMapScene:
                    targetScene = new RoguelikeMapScene(sceneType, "RoguelikeMapScene", param);
                    break;
                default:
                    Debug.Log("场景加载失败");
                    break;
            }

            if (targetScene == null)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            this.currentScene = targetScene;
            // 战斗进关：先绑 Unity 场景壳，再 Init 里 BeginSession
            this.currentScene.InitSceneObject();
            this.eventDispatcher.DispatchEvent(SceneEventType.LOAD_SCENE_FINISH);
            this.currentScene.Init();

            // Session 未成功则回滚，避免空场景停留在 BattleScene
            if (sceneType == BaseSceneType.BattleScene &&
                (ServerProvision.battleSessionHost == null ||
                 !ServerProvision.battleSessionHost.HasActiveSession))
            {
                BattleFlowLog.Abort("LoadSceneProgress", "BattleScene Init 未能开启 Session，回滚");
                BaseScene failedScene = this.currentScene;
                this.currentScene = null;
                failedScene.Dispose();
                onComplete?.Invoke(false);
                yield break;
            }

            ViewManager.Instance?.SetShowPanelActive(true);

            onComplete?.Invoke(true);
        }

        bool TryEnsureUnitySceneActive(string unitySceneName, out string error)
        {
            error = null;
            Scene targetScene = SceneLoader.FindLoadedSceneByName(unitySceneName);
            if (!targetScene.IsValid())
            {
                Scene active = SceneManager.GetActiveScene();
                error = unitySceneName +
                        " 未加载; active=" + active.name +
                        " sceneCount=" + SceneManager.sceneCount +
                        " loaded=[" + SceneLoader.BuildLoadedSceneNameList() + "]";
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene != targetScene)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            return true;
        }

        void BindUICameraFromActiveScene()
        {
            if (ViewManager.Instance != null)
            {
                ViewManager.Instance.RebindScenePresentation();
                this.uiCamera = ViewManager.Instance.GetUICamera();
                if (this.uiCamera != null)
                {
                    return;
                }
            }

            this.uiCamera = UIPresentationPersistence.EnsureGlobalUiCamera();
            if (this.uiCamera == null)
            {
                Debug.LogWarning("[SceneServer] 当前场景中未找到名为 UICamera 的物体。");
            }
        }

        public void Dispose()
        {
        }
    }
}
