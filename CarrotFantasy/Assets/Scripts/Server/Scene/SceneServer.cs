using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarrotFantasy
{
    public class SceneServer
    {
        private EventDispatcher eventDispatcher;
        private Camera uiCamera; //固定的
        private Camera mainCamera; // 主摄像机（主要是拍3D物体）
        public BaseScene currentScene;

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

        //public Dictionary<PanelLayerType, GameObject> GetPanelLayerInfo()
        //{
        //    return this.currentScene.getLayerDic();
        //}

        private void RemoveScene()
        {
            ViewManager.Instance.CloseAllPanel(PanelCloseReasonType.SCENE_CHANGE, this.currentScene.sceneType);
            ViewManager.Instance.SetShowPanelActive(false);
            this.currentScene.Dispose(); //卸载旧场景
        }

        public bool LoadScene(BaseSceneType sceneType, Dictionary<String, dynamic> param)
        {
            bool isLoad = false;
            if (this.currentScene != null)
            {
                if (this.currentScene.sceneType == sceneType) return isLoad;
                this.RemoveScene();
            }
            //ResourceLoader.Instance.setSceneType(sceneType); 切换场景，卸载旧场景资源（）
            isLoad = this.LoadSceneProgress(sceneType, param);
            return isLoad;
        }

        private bool LoadSceneProgress(BaseSceneType sceneType, Dictionary<String, dynamic> param)
        {
            GameSceneType unitySceneType = SceneLoader.ToGameSceneType(sceneType);
            string unitySceneName = SceneLoader.ToSceneName(unitySceneType);
            if (!string.IsNullOrEmpty(unitySceneName))
            {
                Scene active = SceneManager.GetActiveScene();
                if (!active.IsValid() || active.name != unitySceneName)
                {
                    SceneLoader.Load(unitySceneType, LoadSceneMode.Single);
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
                    targetScene = new BattleScene(sceneType, "BattleScene", param);
                    break;
                default:
                    Debug.Log("场景加载失败");
                    break;
            }
            if (targetScene == null)
            {
                return false;
            }
            this.currentScene = targetScene;

            //this.mainCamera = currentScene.GetMainCamera();
            //if (this.mainCamera != null)
            //{
            //    this.uiCamera.clearFlags = CameraClearFlags.Depth;
            //}
            //else
            //{
            //    this.uiCamera.clearFlags = CameraClearFlags.Color;
            //}

            this.currentScene.InitSceneObject();

            //ViewManager.Instance.SetShowPanelActive(true); //其实不一定需要这句
            this.eventDispatcher.DispatchEvent(SceneEventType.LOAD_SCENE_FINISH);

            this.currentScene.Init();
            return true;
        }

        /// <summary>
        /// Unity 场景切换后重新绑定 UI 摄像机（场景内对象会被卸载重建）。
        /// </summary>
        private void BindUICameraFromActiveScene()
        {
            GameObject uiCameraGo = GameObject.Find("UICamera");
            if (uiCameraGo == null)
            {
                Debug.LogWarning("[SceneServer] 当前场景中未找到名为 UICamera 的物体。");
                this.uiCamera = null;
                return;
            }

            this.uiCamera = uiCameraGo.GetComponent<Camera>();
            if (this.uiCamera == null)
            {
                Debug.LogWarning("[SceneServer] UICamera 上未找到 Camera 组件。");
            }
        }

        public void Dispose()
        {

        }

    }
}
