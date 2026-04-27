using System;
using System.Collections.Generic;
using UnityEngine;

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
        }

        public void InitSceneObject()
        {

        }

        public virtual void Init()
        {

        }

        public BaseSceneType GetSceneType()
        {
            return sceneType;
        }

        public virtual Camera GetMainCamera()
        {
            return null;
        }

        public virtual void Dispose()
        {
            GameObject.Destroy(gameObj);
        }
    }
}
