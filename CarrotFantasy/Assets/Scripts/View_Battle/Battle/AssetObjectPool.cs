using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class AssetObjectPool
    {
        private static AssetObjectPool assetObjectPool;
        private Dictionary<String, GameObject> path2Asset = new Dictionary<string, GameObject>();
        private Dictionary<String, UnityEngine.Object> path2Asset2 = new Dictionary<string, UnityEngine.Object>();

        public static AssetObjectPool Instance
        {
            get
            {
                if (assetObjectPool == null)
                {
                    assetObjectPool = new AssetObjectPool();
                }
                return assetObjectPool;
            }
        }

        public GameObject getAsset(String path)
        {
            if (this.path2Asset[path] == null)
            {
                this.path2Asset[path] = ResourceLoader.Instance.getGameObject(path);
            }
            return this.path2Asset[path];
        }

        public T getAsset<T>(String path) where T : UnityEngine.Object
        {
            if (this.path2Asset2[path] == null)
            {
                this.path2Asset2[path] = ResourceLoader.Instance.loadRes<T>(path);
            }
            return (T)this.path2Asset2[path];
        }

        public void Dispose()
        {
            this.path2Asset.Clear();
            this.path2Asset2.Clear();
        }
    }
}
