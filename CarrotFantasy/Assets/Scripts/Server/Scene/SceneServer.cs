using System;
using System.Collections.Generic;
using UnityEngine;

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
            GameObject uiCamera = GameObject.Find("uiCamera");
            this.uiCamera = uiCamera.GetComponent<Camera>();
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

        public Dictionary<PanelLayerType, GameObject> GetPanelLayerInfo()
        {
            return this.currentScene.getLayerDic();
        }

        private void RemoveScene()
        {
            ServerProvision.panelServer.CloseAllPanel(PanelCloseReasonType.SCENE_CHANGE, this.currentScene.sceneType);
            ServerProvision.panelServer.SetShowPanelActive(false);
            this.currentScene.Dispose(); //卸载旧场景
        }

        public bool LoadScene(BaseSceneType sceneType, Dictionary<String, dynamic> param)
        {
            bool isLoad = false;
            if(this.currentScene != null)
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
            BaseScene targetScene = null;
            switch (sceneType) {
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
            if(targetScene == null)
            {
                return false;
            }
            this.currentScene = targetScene;
            this.mainCamera = currentScene.getMainCamera();
            if (this.mainCamera != null)
            {
                this.uiCamera.clearFlags = CameraClearFlags.Depth;
            }
            else
            {
                this.uiCamera.clearFlags = CameraClearFlags.Color;
            }
            this.currentScene.initSceneObject();

            ServerProvision.panelServer.SetShowPanelActive(true); //其实不一定需要这句
            this.eventDispatcher.DispatchEvent(SceneEventType.LOAD_SCENE_FINISH);

            this.currentScene.init();
            return true;
        }

        public void Dispose()
        {

        }

    }
}
