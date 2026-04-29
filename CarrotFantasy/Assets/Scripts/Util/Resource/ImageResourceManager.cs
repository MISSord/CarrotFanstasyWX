using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 图片资源统一入口：通过句柄管理生命周期，避免图片资源只增不减。
    /// </summary>
    public sealed class ImageResourceManager
    {
        private sealed class ImageLoadRequest
        {
            public int Id;
            public string BundleName;
            public string AssetName;
            public int CallbackId;
            public bool IsReleased;
            public bool IsLoaded;
            public Action<UnityEngine.Object> Callback;
        }

        private static ImageResourceManager _instance;
        public static ImageResourceManager Instance => _instance ?? (_instance = new ImageResourceManager());

        private readonly Dictionary<int, ImageLoadRequest> _requestMap = new Dictionary<int, ImageLoadRequest>();

        private int _nextId = 1;

        private ImageResourceManager()
        {
        }

        public ImageLoadHandle LoadSprite(string bundleName, string assetName, Action<Sprite> onLoaded, LoadPriority priority = LoadPriority.Medium)
        {
            return LoadAssetInternal<Sprite>(bundleName, assetName, obj => onLoaded?.Invoke(obj as Sprite), "LoadSprite", priority);
        }

        public ImageLoadHandle LoadTexture(string bundleName, string assetName, Action<Texture> onLoaded, LoadPriority priority = LoadPriority.Medium)
        {
            return LoadAssetInternal<Texture>(bundleName, assetName, obj => onLoaded?.Invoke(obj as Texture), "LoadTexture", priority);
        }

        private ImageLoadHandle LoadAssetInternal<T>(string bundleName, string assetName, Action<UnityEngine.Object> onLoaded, string logName, LoadPriority priority)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                GameLogController.Error($"{logName}失败，bundleName或assetName为空", "ImageResourceManager");
                onLoaded?.Invoke(null);
                return ImageLoadHandle.Invalid;
            }

            if (AssetBundleManager.Instance == null)
            {
                GameLogController.Error($"{logName}失败，AssetBundleManager未初始化", "ImageResourceManager");
                onLoaded?.Invoke(null);
                return ImageLoadHandle.Invalid;
            }

            int requestId = _nextId++;
            var request = new ImageLoadRequest
            {
                Id = requestId,
                BundleName = bundleName,
                AssetName = assetName,
                CallbackId = -1,
                IsReleased = false,
                IsLoaded = false,
                Callback = onLoaded
            };

            _requestMap[requestId] = request;
            request.CallbackId = AssetBundleManager.Instance.LoadAsset<T>(
                bundleName,
                assetName,
                asset => OnAssetLoaded(requestId, asset),
                priority);

            return new ImageLoadHandle(this, requestId);
        }

        internal void Release(int requestId)
        {
            if (!_requestMap.TryGetValue(requestId, out ImageLoadRequest request))
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
            if (!_requestMap.TryGetValue(requestId, out ImageLoadRequest request))
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

    public struct ImageLoadHandle : IDisposable
    {
        public static readonly ImageLoadHandle Invalid = new ImageLoadHandle(null, -1);

        private readonly ImageResourceManager _manager;
        private readonly int _requestId;

        internal ImageLoadHandle(ImageResourceManager manager, int requestId)
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
}
