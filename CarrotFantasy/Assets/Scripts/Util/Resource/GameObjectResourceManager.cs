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

        /// <summary>
        /// 阻塞加载预制体模板（驱动 <see cref="AssetBundleManager.Update"/>），用于必须在 Init 内完成的同步初始化。
        /// 调用方在不再需要模板引用时必须 <see cref="AssetLoadHandle.Dispose"/>。
        /// </summary>
        public GameObject LoadPrefabBlocking(string bundleName, string assetName, out AssetLoadHandle handle, int maxTicks = 480)
        {
            handle = AssetLoadHandle.Invalid;
            GameObject result = null;
            handle = LoadPrefab(bundleName, assetName, go => result = go, LoadPriority.Sync);
            if (!handle.IsValid)
            {
                return null;
            }

            PumpLoadsUntil(ref result, maxTicks);
            if (result == null)
            {
                handle.Dispose();
                handle = AssetLoadHandle.Invalid;
            }

            return result;
        }

        private static void PumpLoadsUntil(ref GameObject result, int maxTicks)
        {
            AssetBundleManager abm = AssetBundleManager.Instance;
            if (abm == null)
            {
                return;
            }

            for (int i = 0; i < maxTicks && result == null; i++)
            {
                abm.Update();
            }
        }
    }
}
