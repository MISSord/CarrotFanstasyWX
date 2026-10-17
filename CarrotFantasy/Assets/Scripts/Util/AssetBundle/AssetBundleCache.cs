using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 加载资源名字拼接，形成唯一的ID
    /// </summary>
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

    /// <summary>
    /// 单个资源的最小管理器，用来管理加载完成后的资源状态。
    /// 用完后放回对象池。仍在缓存字典里，或还有句柄指着它时，不能回池。
    /// </summary>
    internal sealed class AbAssetProvider
    {
        private static readonly ObjectPool<AbAssetProvider> Pool = new ObjectPool<AbAssetProvider>(16, 256, Reset);

        /// <summary>从池里取出并写成一次新的加载。调用方放入缓存字典后把 <see cref="InCache"/> 设为 true。</summary>
        internal static AbAssetProvider Rent(AbProviderKey key, IAbCacheHost host)
        {
            AbAssetProvider provider = Pool.Get();
            provider._pooled = false;
            provider.Key = key;
            provider.State = AbProviderState.Loading;
            provider.Asset = null;
            provider.RefCount = 0;
            provider.SourceStarted = false;
            provider.SourceHeld = false;
            provider.CallbackId = -1;
            provider.LoadFinished = false;
            provider.HoldsAssetRef = false;
            provider.IdleSince = -1f;
            provider.FailUntil = -1f;
            provider.Host = host;
            provider.InCache = false;
            provider.Waiters.Clear();
            return provider;
        }

        /// <summary>字典和句柄都不再使用时放回池。重复调用无操作。</summary>
        internal void RecycleIfUnused()
        {
            if (_pooled || InCache || RefCount > 0)
            {
                return;
            }

            _pooled = true;
            Pool.Release(this);
        }

        private static void Reset(AbAssetProvider provider)
        {
            provider.Key = default;
            provider.State = AbProviderState.Loading;
            provider.Asset = null;
            provider.RefCount = 0;
            provider.SourceStarted = false;
            provider.SourceHeld = false;
            provider.CallbackId = -1;
            provider.LoadFinished = false;
            provider.HoldsAssetRef = false;
            provider.IdleSince = -1f;
            provider.FailUntil = -1f;
            provider.Host = null;
            provider.InCache = false;
            provider.Waiters.Clear();
            provider._pooled = true;
        }

        private bool _pooled;

        /// <summary>包名、资源名、类型。同一个 Key 在一个缓存里只有这一条记录。</summary>
        public AbProviderKey Key;

        /// <summary>Loading 还在等管理器；Succeeded 已拿到资源；Failed 这次失败。</summary>
        public AbProviderState State;

        /// <summary>加载成功后的资源对象。失败或尚未完成时为 null。</summary>
        public UnityEngine.Object Asset;

        /// <summary>还没 Release 的句柄数量。降到 0 后才能进入空闲，等待 ReleaseIdle 归还。</summary>
        public int RefCount;

        /// <summary>是否已经向 AssetBundleManager 发起过这次加载。防止同一个 Key 重复 LoadAsset。</summary>
        public bool SourceStarted;

        /// <summary>缓存是否还持有这次加载。为 false 时回调到来只卸引用，不再通知句柄。</summary>
        public bool SourceHeld;

        /// <summary>管理器返回的回调编号。-1 表示没有可取消的回调。</summary>
        public int CallbackId = -1;

        /// <summary>管理器回调是否已经到达。到达前取消走 CancelAssetLoad，到达后归还走 UnloadAsset。</summary>
        public bool LoadFinished;

        /// <summary>回调带回了非空资源，释放时需要 UnloadAsset。失败时管理器已经减过引用，这里为 false。</summary>
        public bool HoldsAssetRef;

        /// <summary>引用降到 0 的时刻（unscaledTime）。-1 表示当前不在空闲计时。</summary>
        public float IdleSince = -1f;

        /// <summary>失败结果保留到这个时刻。未到之前再次 Load 直接失败，不再向管理器请求。</summary>
        public float FailUntil = -1f;

        /// <summary>持有这条记录的缓存。句柄 Release 时通过它减引用。</summary>
        public IAbCacheHost Host;

        /// <summary>是否还在缓存字典里。移出字典且引用计数为 0 后才能回池。</summary>
        public bool InCache;

        /// <summary>第一次加载尚未完成时，正在等待结果的句柄。</summary>
        public readonly List<IAbWaiter> Waiters = new List<IAbWaiter>();
    }

    internal struct AssetBundleCachePolicy
    {
        public float IdleSeconds;
        public bool RememberFailure;
        public float FailureRememberSeconds;
    }

    /// <summary>
    /// 一个加载器内部的缓存。同一个 (包, 资源, 类型) 只有一个 Provider。
    /// </summary>
    internal sealed class AssetBundleCache<T> : IAbCacheHost where T : UnityEngine.Object
    {
        private readonly string _name;
        private readonly AssetBundleCachePolicy _policy;
        private readonly Dictionary<AbProviderKey, AbAssetProvider> _providers = new Dictionary<AbProviderKey, AbAssetProvider>();

        public AssetBundleCache(string name, AssetBundleCachePolicy policy)
        {
            _name = name;
            _policy = policy;
        }

        public AssetBundleHandle<T> Load(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            AssetBundleHandle<T> handle = AssetBundleHandle<T>.Rent();
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
                failed.InCache = false;
                failed.RecycleIfUnused();
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
                StartLoad(key, priority);
            }

            return handle;
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

            provider.RecycleIfUnused();
        }

        private void StartLoad(AbProviderKey key, LoadPriority priority)
        {
            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null || !_providers.TryGetValue(key, out AbAssetProvider provider))
            {
                ApplySourceLoaded(key, null);
                return;
            }

            // 管理器不会在 LoadAsset 返回前调用这个回调。已在内存中的资源也要等下一帧 Update。
            provider.CallbackId = manager.LoadAsset(key.Bundle, key.Asset, key.AssetType, asset =>
            {
                OnManagerLoaded(key, manager, asset);
            }, priority);
        }

        private void OnManagerLoaded(AbProviderKey key, AssetBundleManager manager, UnityEngine.Object asset)
        {
            if (!_providers.TryGetValue(key, out AbAssetProvider provider) || !provider.SourceHeld)
            {
                if (asset != null)
                {
                    manager.UnloadAsset(key.Bundle, key.Asset);
                }

                return;
            }

            provider.LoadFinished = true;
            provider.HoldsAssetRef = asset != null;
            ApplySourceLoaded(key, asset);
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
            SnapshotWaiters(provider.Waiters, out IAbWaiter[] waiters, out int[] leases);
            DeliverWaiters(waiters, leases, asset);
        }

        private void FailProvider(AbProviderKey key, AbAssetProvider provider)
        {
            provider.State = AbProviderState.Failed;
            provider.Asset = null;
            provider.FailUntil = _policy.RememberFailure
                ? Time.unscaledTime + _policy.FailureRememberSeconds
                : 0f;
            SnapshotWaiters(provider.Waiters, out IAbWaiter[] waiters, out int[] leases);
            ReleaseSource(provider);
            provider.SourceStarted = false;
            if (!_policy.RememberFailure)
            {
                _providers.Remove(key);
                provider.InCache = false;
            }

            DeliverWaiters(waiters, leases, null);
            provider.RecycleIfUnused();
        }

        /// <summary>
        /// 先记下每个句柄的租用编号再清空列表。回调里 Release 会把句柄放回池，编号变了就不能再 Complete。
        /// </summary>
        private static void SnapshotWaiters(List<IAbWaiter> source, out IAbWaiter[] waiters, out int[] leases)
        {
            waiters = source.ToArray();
            leases = new int[waiters.Length];
            for (int i = 0; i < waiters.Length; i++)
            {
                leases[i] = waiters[i].LeaseId;
            }

            source.Clear();
        }

        private static void DeliverWaiters(IAbWaiter[] waiters, int[] leases, UnityEngine.Object asset)
        {
            for (int i = 0; i < waiters.Length; i++)
            {
                IAbWaiter waiter = waiters[i];
                if (waiter.LeaseId != leases[i] || waiter.IsReleased)
                {
                    continue;
                }

                waiter.Complete(asset);
            }
        }

        private void DropProvider(AbProviderKey key, AbAssetProvider provider)
        {
            provider.Waiters.Clear();
            _providers.Remove(key);
            provider.InCache = false;
            ReleaseSource(provider);
            provider.RecycleIfUnused();
        }

        private void ReleaseSource(AbAssetProvider provider)
        {
            if (!provider.SourceHeld)
            {
                return;
            }

            provider.SourceHeld = false;
            AssetBundleManager manager = AssetBundleManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (provider.LoadFinished)
            {
                // 加载失败时管理器已经减过引用，这里只归还真正拿到的资源。
                if (provider.HoldsAssetRef)
                {
                    manager.UnloadAsset(provider.Key.Bundle, provider.Key.Asset);
                }
            }
            else if (provider.CallbackId >= 0)
            {
                manager.CancelAssetLoad(provider.Key.Bundle, provider.Key.Asset, provider.CallbackId);
            }
        }

        private AbAssetProvider GetOrCreateProvider(AbProviderKey key)
        {
            if (_providers.TryGetValue(key, out AbAssetProvider existing) && existing.State != AbProviderState.Failed)
            {
                return existing;
            }

            AbAssetProvider created = AbAssetProvider.Rent(key, this);
            created.InCache = true;
            _providers[key] = created;
            return created;
        }
    }
}
