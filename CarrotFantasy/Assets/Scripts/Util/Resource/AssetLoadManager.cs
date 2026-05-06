using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 通用资源加载句柄：通过 Dispose 配对 UnloadAsset / CancelAssetLoad，避免引用只增不减。
    /// </summary>
    public struct AssetLoadHandle : IDisposable
    {
        public static readonly AssetLoadHandle Invalid = new AssetLoadHandle(null, -1);

        private readonly AssetLoadManager _manager;
        private readonly int _requestId;

        internal AssetLoadHandle(AssetLoadManager manager, int requestId)
        {
            _manager = manager;
            _requestId = requestId;
        }

        public bool IsValid => _manager != null && _requestId >= 0;

        public void Dispose()
        {
            if (!IsValid)
            {
                return;
            }

            _manager.Release(_requestId);
        }
    }

    /// <summary>
    /// 从 AssetBundle 异步加载 UnityEngine.Object 的统一入口（图片、预制体等共用生命周期模型）。
    /// </summary>
    public sealed class AssetLoadManager
    {
        private sealed class AssetLoadRequest
        {
            public int Id;
            public string BundleName;
            public string AssetName;
            public int CallbackId;
            public bool IsReleased;
            public bool IsLoaded;
            public Action<UnityEngine.Object> Callback;
        }

        private static AssetLoadManager _instance;
        public static AssetLoadManager Instance => _instance ?? (_instance = new AssetLoadManager());

        private readonly Dictionary<int, AssetLoadRequest> _requestMap = new Dictionary<int, AssetLoadRequest>();

        private int _nextId = 1;

        private const string LogModule = "AssetLoadManager";

        private AssetLoadManager()
        {
        }

        public AssetLoadHandle LoadAsset<T>(string bundleName, string assetName, Action<T> onLoaded, LoadPriority priority = LoadPriority.Medium, string operationName = null)
            where T : UnityEngine.Object
        {
            string logName = operationName ?? $"LoadAsset<{typeof(T).Name}>";

            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                GameLogController.Error($"{logName}失败，bundleName或assetName为空", LogModule);
                onLoaded?.Invoke(null);
                return AssetLoadHandle.Invalid;
            }

            if (AssetBundleManager.Instance == null)
            {
                GameLogController.Error($"{logName}失败，AssetBundleManager未初始化", LogModule);
                onLoaded?.Invoke(null);
                return AssetLoadHandle.Invalid;
            }

            int requestId = _nextId++;
            var request = new AssetLoadRequest
            {
                Id = requestId,
                BundleName = bundleName,
                AssetName = assetName,
                CallbackId = -1,
                IsReleased = false,
                IsLoaded = false,
                Callback = obj => onLoaded?.Invoke(obj as T)
            };

            _requestMap[requestId] = request;
            request.CallbackId = AssetBundleManager.Instance.LoadAsset<T>(
                bundleName,
                assetName,
                asset => OnAssetLoaded(requestId, asset),
                priority);

            return new AssetLoadHandle(this, requestId);
        }

        internal void Release(int requestId)
        {
            if (!_requestMap.TryGetValue(requestId, out AssetLoadRequest request))
            {
                return;
            }

            if (request.IsReleased)
            {
                return;
            }

            request.IsReleased = true;

            if (AssetBundleManager.Instance != null)
            {
                if (request.IsLoaded)
                {
                    AssetBundleManager.Instance.UnloadAsset(request.BundleName, request.AssetName);
                }
                else if (request.CallbackId >= 0)
                {
                    AssetBundleManager.Instance.CancelAssetLoad(request.BundleName, request.AssetName, request.CallbackId);
                }
            }

            _requestMap.Remove(requestId);
        }

        private void OnAssetLoaded(int requestId, UnityEngine.Object asset)
        {
            if (!_requestMap.TryGetValue(requestId, out AssetLoadRequest request))
            {
                return;
            }

            if (request.IsReleased)
            {
                _requestMap.Remove(requestId);
                return;
            }

            request.IsLoaded = true;
            request.Callback?.Invoke(asset);
            request.Callback = null;
        }
    }
}
