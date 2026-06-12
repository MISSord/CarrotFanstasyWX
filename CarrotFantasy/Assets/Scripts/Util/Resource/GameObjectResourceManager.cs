using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 预制体等资源入口（GameObject），内部委托 <see cref="AssetLoadManager"/>。
    /// </summary>
    public sealed class GameObjectResourceManager
    {
        private static GameObjectResourceManager _instance;
        public static GameObjectResourceManager Instance => _instance ?? (_instance = new GameObjectResourceManager());

        private GameObjectResourceManager()
        {
        }

        public AssetLoadHandle LoadPrefab(string bundleName, string assetName, Action<GameObject> onLoaded, LoadPriority priority = LoadPriority.Medium)
        {
            return AssetLoadManager.Instance.LoadAsset<GameObject>(bundleName, assetName, onLoaded, priority, "LoadPrefab");
        }
    }
}
