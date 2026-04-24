using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BaseScene
    {
        public BaseSceneType sceneType;
        public GameObject gameObj;
        private Dictionary<PanelLayerType, GameObject> layerDic = new Dictionary<PanelLayerType, GameObject>();
        protected String prefabUrl = "";

        public BaseScene(BaseSceneType type, String name, Dictionary<String, dynamic> param)
        {
            sceneType = type;
            this.gameObj = new GameObject(name);
            this.gameObj.transform.position = Vector3.zero;
            this.gameObj.transform.SetSiblingIndex(0);

            GameObject mainCanvas = new GameObject("MainCanvas");
            mainCanvas.transform.SetParent(gameObj.transform, false);
            Canvas canvas = mainCanvas.AddComponent<Canvas>();
            canvas.sortingOrder = 30;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ServerProvision.sceneServer.GetUICamera();

            CanvasScaler canvasScaler = mainCanvas.AddComponent<CanvasScaler>();
            UIUtil.Instance.initCanvasScale(canvasScaler);
            canvasScaler.enabled = false;
            canvasScaler.enabled = true; //强制刷新一次

            foreach (KeyValuePair<PanelLayerType, bool> layerData in SceneLayerData.sceneLayerSetting)
            {
                GameObject layer = new GameObject(SceneLayerData.sceneLayerName[layerData.Key]);
                CanvasScaler scaler = layer.AddComponent<CanvasScaler>();
                if (layerData.Value == true)
                {
                    layer.layer = SceneLayerData.layerType[1]; // UI层
                    //Canvas canvasOne = layer.AddComponent<Canvas>();
                    RectTransform rect = layer.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;

                    if (UIUtil.Instance.getMatchWidthOrHeighRatio() == 0)// 未来可能做屏幕适配
                    {
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                    }
                    else
                    {
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                    }

                    layer.AddComponent<GraphicRaycaster>();
                    layer.transform.SetParent(mainCanvas.transform, false);
                }
                else
                {
                    layer.transform.SetParent(gameObj.transform, false);
                }
                layerDic.Add(layerData.Key, layer);
            }

            foreach (KeyValuePair<PanelLayerType, GameObject> layerData in layerDic)
            {
                layerData.Value.transform.SetAsLastSibling();
                layerData.Value.transform.localPosition = Vector3.zero;
            }
            
        }

        public void initSceneObject()
        {
            GameObject sceneObject;
            if (this.prefabUrl != null)
            {
                sceneObject = GameObject.Instantiate(ResourceLoader.Instance.getGameObject(prefabUrl), gameObj.transform, false);
            }
            else
            {
                sceneObject = new GameObject("defaultOne");
            }
            sceneObject.transform.SetParent(this.gameObj.transform, false);
            sceneObject.transform.localPosition = Vector3.zero;
            sceneObject.transform.SetAsFirstSibling();
        }

        public virtual void init()
        {

        }

        public virtual GameObject getLayerGameObj(PanelLayerType type)
        {
            if(layerDic[type] != null)
            {
                return layerDic[type];
            }
            return null;
        }

        public Dictionary<PanelLayerType, GameObject> getLayerDic()
        {
            return layerDic;
        }

        public BaseSceneType getSceneType()
        {
            return sceneType;
        }

        public virtual Camera getMainCamera()
        {
            return null;
        }

        public virtual void Dispose()
        {
            GameObject.Destroy(gameObj);
        }
    }
}
