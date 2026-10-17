using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    internal readonly struct AbProviderKey : IEquatable<AbProviderKey>
    {
        public readonly string Bundle;
        public readonly string Asset;
        public readonly Type AssetType;

        public AbProviderKey(string bundle, string asset, Type assetType)
        {
            Bundle = bundle ?? string.Empty;
            Asset = asset ?? string.Empty;
            AssetType = assetType;
        }

        public static AbProviderKey Create(string bundleName, string assetName, Type assetType)
        {
            string bundle = string.IsNullOrEmpty(bundleName)
                ? string.Empty
                : bundleName.Trim().Replace('\\', '/').ToLowerInvariant();
            string asset = string.IsNullOrEmpty(assetName) ? string.Empty : assetName.Trim();
            return new AbProviderKey(bundle, asset, assetType);
        }

        public bool IsEmpty => string.IsNullOrEmpty(Bundle) || string.IsNullOrEmpty(Asset) || AssetType == null;

        public bool Equals(AbProviderKey other)
        {
            return Bundle == other.Bundle && Asset == other.Asset && AssetType == other.AssetType;
        }

        public override bool Equals(object obj)
        {
            return obj is AbProviderKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Bundle.GetHashCode();
                hash = (hash * 397) ^ Asset.GetHashCode();
                hash = (hash * 397) ^ (AssetType == null ? 0 : AssetType.GetHashCode());
                return hash;
            }
        }

        public override string ToString()
        {
            string typeName = AssetType == null ? "?" : AssetType.Name;
            return typeName + " " + Bundle + " / " + Asset;
        }
    }

    internal enum AbProviderState
    {
        Loading = 0,
        Succeeded = 1,
        Failed = 2
    }

    internal interface IAbCacheHost
    {
        void NotifyHandleReleased(IAbWaiter waiter, AbAssetProvider provider);
    }

    internal sealed class AbAssetProvider
    {
        public AbProviderKey Key;
        public AbProviderState State;
        public UnityEngine.Object Asset;
        public int RefCount;
        public bool SourceStarted;
        public bool SourceHeld;
        public float IdleSince = -1f;
        public float FailUntil = -1f;
        public IAbCacheHost Host;
        public readonly List<IAbWaiter> Waiters = new List<IAbWaiter>();
    }

    internal struct AssetBundleCachePolicy
    {
        public float IdleSeconds;
        public bool RememberFailure;
        public float FailureRememberSeconds;

        public static AssetBundleCachePolicy Prefab()
        {
            return new AssetBundleCachePolicy
            {
                IdleSeconds = 120f,
                RememberFailure = true,
                FailureRememberSeconds = 5f
            };
        }

        public static AssetBundleCachePolicy Image()
        {
            return new AssetBundleCachePolicy
            {
                IdleSeconds = 15f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            };
        }

        public static AssetBundleCachePolicy Audio()
        {
            return new AssetBundleCachePolicy
            {
                IdleSeconds = 30f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            };
        }

        public static AssetBundleCachePolicy Text()
        {
            return new AssetBundleCachePolicy
            {
                IdleSeconds = 0f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            };
        }
    }

    internal interface IAbAssetSource
    {
        void Load(AbProviderKey key, LoadPriority priority, Action<UnityEngine.Object> onLoaded);

        void Release(AbProviderKey key);

        void Pump();
    }

    /// <summary>
    /// 真机与编辑器生产模式。加载失败时 AssetBundleManager 已经减过引用，这里不能再 UnloadAsset。
    /// </summary>
    internal sealed class LegacyAbAssetSource : IAbAssetSource
    {
        private sealed class LoadReq
        {
            public int CallbackId = -1;
            public bool Finished;
            public bool Released;
            public bool HoldRef;
        }

        private readonly Dictionary<AbProviderKey, LoadReq> _loads = new Dictionary<AbProviderKey, LoadReq>();

        public void Load(AbProviderKey key, LoadPriority priority, Action<UnityEngine.Object> onLoaded)
        {
            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null)
            {
                onLoaded?.Invoke(null);
                return;
            }

            LoadReq req = new LoadReq();
            _loads[key] = req;
            req.CallbackId = manager.LoadAsset(key.Bundle, key.Asset, key.AssetType, asset =>
            {
                req.Finished = true;
                req.HoldRef = asset != null;
                if (req.Released)
                {
                    if (req.HoldRef)
                    {
                        manager.UnloadAsset(key.Bundle, key.Asset);
                    }

                    return;
                }

                onLoaded?.Invoke(asset);
            }, priority);
        }

        public void Release(AbProviderKey key)
        {
            if (!_loads.TryGetValue(key, out LoadReq req))
            {
                return;
            }

            _loads.Remove(key);
            req.Released = true;
            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (req.Finished)
            {
                if (req.HoldRef)
                {
                    manager.UnloadAsset(key.Bundle, key.Asset);
                }
            }
            else if (req.CallbackId >= 0)
            {
                manager.CancelAssetLoad(key.Bundle, key.Asset, req.CallbackId);
            }
        }

        public void Pump()
        {
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器 Development / DebugMode。第一次加载在 Pump 里回调。
    /// </summary>
    internal sealed class EditorAbAssetSource : IAbAssetSource
    {
        private struct Pending
        {
            public AbProviderKey Key;
            public Action<UnityEngine.Object> Callback;
        }

        private readonly List<Pending> _queue = new List<Pending>();
        private readonly HashSet<AbProviderKey> _alive = new HashSet<AbProviderKey>();

        public void Load(AbProviderKey key, LoadPriority priority, Action<UnityEngine.Object> onLoaded)
        {
            _alive.Add(key);
            _queue.Add(new Pending { Key = key, Callback = onLoaded });
        }

        public void Release(AbProviderKey key)
        {
            _alive.Remove(key);
        }

        public void Pump()
        {
            if (_queue.Count == 0)
            {
                return;
            }

            Pending[] batch = _queue.ToArray();
            _queue.Clear();
            for (int i = 0; i < batch.Length; i++)
            {
                Pending item = batch[i];
                if (!_alive.Contains(item.Key))
                {
                    continue;
                }

                UnityEngine.Object asset = EditorAssetLoader.LoadAssetAtPath(item.Key.Bundle, item.Key.Asset, item.Key.AssetType);
                item.Callback?.Invoke(asset);
            }
        }
    }
#endif

    internal static class AbAssetSources
    {
        private static IAbAssetSource _shared;
        private static bool _inited;

        public static IAbAssetSource Shared
        {
            get
            {
                Init();
                return _shared;
            }
        }

        public static void Init()
        {
            if (_inited)
            {
                return;
            }

            _shared = Create();
            _inited = true;
        }

        public static void Pump()
        {
            if (_inited)
            {
                _shared.Pump();
            }
        }

        private static IAbAssetSource Create()
        {
#if UNITY_EDITOR
            LoadMode mode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
            if (mode == LoadMode.Development || mode == LoadMode.DebugMode)
            {
                return new EditorAbAssetSource();
            }
#endif
            return new LegacyAbAssetSource();
        }
    }

    /// <summary>
    /// 一个加载器内部的缓存。同一个 (包, 资源, 类型) 只有一个 Provider。
    /// </summary>
    internal sealed class AssetBundleCache<T> : IAbCacheHost where T : UnityEngine.Object
    {
        private struct PendingResult
        {
            public AbProviderKey Key;
            public UnityEngine.Object Asset;
        }

        private readonly string _name;
        private readonly AssetBundleCachePolicy _policy;
        private readonly Dictionary<AbProviderKey, AbAssetProvider> _providers = new Dictionary<AbProviderKey, AbAssetProvider>();
        private readonly List<PendingResult> _pending = new List<PendingResult>();

        public AssetBundleCache(string name, AssetBundleCachePolicy policy)
        {
            _name = name;
            _policy = policy;
        }

        public AssetBundleHandle<T> Load(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            AbAssetSources.Init();
            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            AssetBundleHandle<T> handle = new AssetBundleHandle<T>();
            if (key.IsEmpty)
            {
                GameLogController.Error(_name + " Load 失败，包名或资源名为空", AssetBundleLoaders.LogTag);
                handle.Complete(null);
                return handle;
            }

            if (_providers.TryGetValue(key, out AbAssetProvider failed) && failed.State == AbProviderState.Failed)
            {
                if (Time.unscaledTime < failed.FailUntil)
                {
                    handle.Complete(null);
                    return handle;
                }

                _providers.Remove(key);
            }

            AbAssetProvider provider = GetOrCreateProvider(key);
            bool startSource = provider.State == AbProviderState.Loading && !provider.SourceStarted;
            handle.Provider = provider;
            provider.RefCount++;
            provider.IdleSince = -1f;
            if (provider.State == AbProviderState.Succeeded)
            {
                handle.Complete(provider.Asset);
                return handle;
            }

            provider.Waiters.Add(handle);
            if (startSource)
            {
                provider.SourceStarted = true;
                provider.SourceHeld = true;
                AbAssetSources.Shared.Load(key, priority, asset => QueueSourceLoaded(key, asset));
            }

            return handle;
        }

        /// <summary>
        /// 第一次加载的完成统一留到这里。编辑器本来就在 Pump 里回调，真机命中已加载资源时也会先排队，避免两端 IsDone 不一致。
        /// 缓存里已经成功的资源仍在 Load 里当场完成。
        /// </summary>
        public void FlushPending()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            PendingResult[] batch = _pending.ToArray();
            _pending.Clear();
            for (int i = 0; i < batch.Length; i++)
            {
                ApplySourceLoaded(batch[i].Key, batch[i].Asset);
            }
        }

        public int ReleaseIdle(bool ignoreDelay)
        {
            List<AbProviderKey> idle = null;
            float now = Time.unscaledTime;
            foreach (KeyValuePair<AbProviderKey, AbAssetProvider> pair in _providers)
            {
                AbAssetProvider provider = pair.Value;
                if (provider.RefCount > 0 || provider.State == AbProviderState.Loading)
                {
                    continue;
                }

                bool expiredFailure = provider.State == AbProviderState.Failed && now >= provider.FailUntil;
                bool idleLongEnough = provider.State == AbProviderState.Succeeded
                    && (ignoreDelay || now - provider.IdleSince >= _policy.IdleSeconds);
                if (!expiredFailure && !idleLongEnough)
                {
                    continue;
                }

                if (idle == null)
                {
                    idle = new List<AbProviderKey>();
                }

                idle.Add(pair.Key);
            }

            if (idle == null)
            {
                return 0;
            }

            for (int i = 0; i < idle.Count; i++)
            {
                if (_providers.TryGetValue(idle[i], out AbAssetProvider provider))
                {
                    DropProvider(idle[i], provider);
                }
            }

            return idle.Count;
        }

        public string Explain(string bundleName)
        {
            string bundle = AbProviderKey.Create(bundleName, "x", typeof(T)).Bundle;
            StringBuilder sb = new StringBuilder();
            sb.Append(_name).Append(" bundle: ").AppendLine(bundle);
            foreach (KeyValuePair<AbProviderKey, AbAssetProvider> pair in _providers)
            {
                if (pair.Key.Bundle != bundle)
                {
                    continue;
                }

                AbAssetProvider provider = pair.Value;
                string state = provider.State.ToString();
                if (provider.State == AbProviderState.Succeeded && provider.RefCount == 0)
                {
                    state = "idle";
                }

                sb.Append("  ").Append(pair.Key.Asset);
                sb.Append("  ref: ").Append(provider.RefCount);
                sb.Append("  state: ").AppendLine(state);
                for (int i = 0; i < AssetBundleScope.All.Count; i++)
                {
                    if (AssetBundleScope.All[i].Keeps(pair.Key))
                    {
                        sb.Append("    scope: ").AppendLine(AssetBundleScope.All[i].Name);
                    }
                }
            }

            return sb.ToString();
        }

        public void NotifyHandleReleased(IAbWaiter waiter, AbAssetProvider provider)
        {
            provider.RefCount = Mathf.Max(0, provider.RefCount - 1);
            provider.Waiters.Remove(waiter);
            if (provider.RefCount == 0 && provider.State == AbProviderState.Succeeded)
            {
                provider.IdleSince = Time.unscaledTime;
            }
        }

        private void QueueSourceLoaded(AbProviderKey key, UnityEngine.Object asset)
        {
            _pending.Add(new PendingResult { Key = key, Asset = asset });
        }

        private void ApplySourceLoaded(AbProviderKey key, UnityEngine.Object asset)
        {
            if (!_providers.TryGetValue(key, out AbAssetProvider provider))
            {
                return;
            }

            if (provider.RefCount <= 0)
            {
                DropProvider(key, provider);
                return;
            }

            if (asset == null || !typeof(T).IsInstanceOfType(asset))
            {
                if (asset != null)
                {
                    GameLogController.Error(_name + " 资源类型不匹配: " + key, AssetBundleLoaders.LogTag);
                }

                FailProvider(key, provider);
                return;
            }

            provider.State = AbProviderState.Succeeded;
            provider.Asset = asset;
            IAbWaiter[] waiters = provider.Waiters.ToArray();
            provider.Waiters.Clear();
            for (int i = 0; i < waiters.Length; i++)
            {
                if (!waiters[i].IsReleased)
                {
                    waiters[i].Complete(asset);
                }
            }
        }

        private void FailProvider(AbProviderKey key, AbAssetProvider provider)
        {
            provider.State = AbProviderState.Failed;
            provider.Asset = null;
            provider.FailUntil = _policy.RememberFailure
                ? Time.unscaledTime + _policy.FailureRememberSeconds
                : 0f;
            IAbWaiter[] waiters = provider.Waiters.ToArray();
            provider.Waiters.Clear();
            ReleaseSource(provider);
            provider.SourceStarted = false;
            if (!_policy.RememberFailure)
            {
                _providers.Remove(key);
            }

            for (int i = 0; i < waiters.Length; i++)
            {
                if (!waiters[i].IsReleased)
                {
                    waiters[i].Complete(null);
                }
            }
        }

        private void DropProvider(AbProviderKey key, AbAssetProvider provider)
        {
            provider.Waiters.Clear();
            _providers.Remove(key);
            ReleaseSource(provider);
        }

        private void ReleaseSource(AbAssetProvider provider)
        {
            if (!provider.SourceHeld)
            {
                return;
            }

            provider.SourceHeld = false;
            AbAssetSources.Shared.Release(provider.Key);
        }

        private AbAssetProvider GetOrCreateProvider(AbProviderKey key)
        {
            if (_providers.TryGetValue(key, out AbAssetProvider existing) && existing.State != AbProviderState.Failed)
            {
                return existing;
            }

            AbAssetProvider created = new AbAssetProvider
            {
                Key = key,
                State = AbProviderState.Loading,
                Host = this
            };
            _providers[key] = created;
            return created;
        }
    }
}
