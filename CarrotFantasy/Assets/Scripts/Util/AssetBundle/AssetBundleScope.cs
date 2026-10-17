using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 一场战斗、一个界面或音频常驻各自持有的句柄集合。
    /// Dispose 只归还句柄，不销毁 GameObject。
    /// </summary>
    public sealed class AssetBundleScope : IDisposable
    {
        private sealed class KeepRecord
        {
            public AbProviderKey Key;
            public bool Done;
            public bool Succeeded;
            public UnityEngine.Object Asset;
            public IDisposable Handle;
        }

        private readonly Dictionary<AbProviderKey, KeepRecord> _keeps = new Dictionary<AbProviderKey, KeepRecord>();
        private readonly List<IDisposable> _loads = new List<IDisposable>();
        private readonly List<AssetBundleBatch> _watchers = new List<AssetBundleBatch>();
        private bool _disposed;

        internal static readonly List<AssetBundleScope> All = new List<AssetBundleScope>();

        public string Name { get; }

        private AssetBundleScope(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "scope" : name;
        }

        public static AssetBundleScope Create(string name)
        {
            AssetBundleScope scope = new AssetBundleScope(name);
            All.Add(scope);
            return scope;
        }

        public AssetBundleHandle<T> Load<T>(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
            where T : UnityEngine.Object
        {
            if (_disposed)
            {
                AssetBundleHandle<T> dead = new AssetBundleHandle<T>();
                dead.Complete(null);
                return dead;
            }

            AssetBundleHandle<T> handle = AssetBundleCaches.Load<T>(bundleName, assetName, priority);
            _loads.Add(handle);
            return handle;
        }

        public void Keep<T>(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
            where T : UnityEngine.Object
        {
            if (_disposed)
            {
                return;
            }

            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            if (key.IsEmpty)
            {
                GameLogController.Error(Name + " Keep 失败，包名或资源名为空", AssetBundleLoaders.LogTag);
                KeepRecord failed = new KeepRecord { Key = key, Done = true };
                DeliverUse(failed);
                Notify(key, false);
                return;
            }

            if (_keeps.TryGetValue(key, out KeepRecord existing))
            {
                if (existing.Done)
                {
                    Notify(key, existing.Succeeded);
                }

                return;
            }

            KeepRecord record = new KeepRecord { Key = key };
            _keeps[key] = record;
            AssetBundleHandle<T> handle = AssetBundleCaches.Load<T>(bundleName, assetName, priority);
            record.Handle = handle;
            handle.Completed += asset => FinishKeep(record, asset);
        }

        public T Get<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            if (_keeps.TryGetValue(key, out KeepRecord record) && record.Done && record.Succeeded)
            {
                return record.Asset as T;
            }

            GameLogController.Error(Name + " Get 失败，资源尚未 Keep 成功: " + key, AssetBundleLoaders.LogTag);
            return null;
        }

        public void Use<T>(string bundleName, string assetName, Action<T> onReady, LoadPriority priority = LoadPriority.Medium)
            where T : UnityEngine.Object
        {
            if (_disposed)
            {
                return;
            }

            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            if (_keeps.TryGetValue(key, out KeepRecord existing) && existing.Done)
            {
                onReady?.Invoke(existing.Succeeded ? existing.Asset as T : null);
                return;
            }

            if (onReady != null)
            {
                _useWaiters.Add(new UseWait<T> { Key = key, Callback = onReady });
            }

            Keep<T>(bundleName, assetName, priority);
        }

        public AssetBundleBatch CreateBatch()
        {
            AssetBundleBatch batch = new AssetBundleBatch(this);
            if (!_watchers.Contains(batch))
            {
                _watchers.Add(batch);
            }

            return batch;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _useWaiters.Clear();

            foreach (KeyValuePair<AbProviderKey, KeepRecord> pair in _keeps)
            {
                pair.Value.Handle?.Dispose();
            }

            _keeps.Clear();

            for (int i = 0; i < _loads.Count; i++)
            {
                _loads[i]?.Dispose();
            }

            _loads.Clear();
            _watchers.Clear();
            All.Remove(this);
        }

        internal void Unwatch(AssetBundleBatch batch)
        {
            _watchers.Remove(batch);
        }

        internal bool Keeps(AbProviderKey key)
        {
            return _keeps.ContainsKey(key);
        }

        internal string Dump()
        {
            return "scope: " + Name + " keeps=" + _keeps.Count + " loads=" + _loads.Count;
        }

        private void FinishKeep(KeepRecord record, UnityEngine.Object asset)
        {
            if (record.Done)
            {
                return;
            }

            record.Done = true;
            record.Succeeded = asset != null;
            record.Asset = asset;
            DeliverUse(record);
            Notify(record.Key, record.Succeeded);
            if (record.Succeeded)
            {
                return;
            }

            _keeps.Remove(record.Key);
            record.Handle?.Dispose();
        }

        private void Notify(AbProviderKey key, bool succeeded)
        {
            AssetBundleBatch[] watchers = _watchers.ToArray();
            for (int i = 0; i < watchers.Length; i++)
            {
                watchers[i].NotifyAsset(key, succeeded);
            }
        }

        private void DeliverUse(KeepRecord record)
        {
            for (int i = _useWaiters.Count - 1; i >= 0; i--)
            {
                UseWait waiter = _useWaiters[i];
                if (!waiter.Key.Equals(record.Key))
                {
                    continue;
                }

                _useWaiters.RemoveAt(i);
                waiter.Invoke(record.Succeeded ? record.Asset : null);
            }
        }

        private readonly List<UseWait> _useWaiters = new List<UseWait>();

        private abstract class UseWait
        {
            public AbProviderKey Key;
            public abstract void Invoke(UnityEngine.Object asset);
        }

        private sealed class UseWait<T> : UseWait where T : UnityEngine.Object
        {
            public Action<T> Callback;

            public override void Invoke(UnityEngine.Object asset)
            {
                Callback?.Invoke(asset as T);
            }
        }
    }

    /// <summary>
    /// 一批 Keep。只负责等待，句柄记在所属 scope 上。
    /// 这一批何时完成，与之后再开的批次无关。
    /// </summary>
    public sealed class AssetBundleBatch
    {
        private struct Item
        {
            public bool Tracked;
            public bool Done;
            public bool Succeeded;
        }

        private readonly AssetBundleScope _scope;
        private readonly Dictionary<AbProviderKey, Item> _assets = new Dictionary<AbProviderKey, Item>();
        private readonly List<string> _failed = new List<string>();
        private Action<bool> _onReady;
        private bool _fired;

        internal AssetBundleBatch(AssetBundleScope scope)
        {
            _scope = scope;
        }

        public void Keep<T>(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
            where T : UnityEngine.Object
        {
            AbProviderKey key = AbProviderKey.Create(bundleName, assetName, typeof(T));
            if (!_assets.ContainsKey(key))
            {
                _assets[key] = new Item { Tracked = true };
            }

            _scope.Keep<T>(bundleName, assetName, priority);
        }

        public void WhenReady(Action<bool> onReady)
        {
            _onReady = onReady;
            TryFire();
        }

        internal void NotifyAsset(AbProviderKey key, bool succeeded)
        {
            if (!_assets.TryGetValue(key, out Item item) || item.Done)
            {
                return;
            }

            item.Done = true;
            item.Succeeded = succeeded;
            _assets[key] = item;
            if (!succeeded)
            {
                _failed.Add(key.ToString());
            }

            TryFire();
        }

        private void TryFire()
        {
            if (_fired || _onReady == null || !AllDone())
            {
                return;
            }

            _fired = true;
            _scope.Unwatch(this);
            bool ok = _failed.Count == 0;
            if (!ok)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(_scope.Name).Append(" 批次失败:");
                for (int i = 0; i < _failed.Count; i++)
                {
                    sb.Append(' ').Append(_failed[i]);
                }

                GameLogController.Error(sb.ToString(), AssetBundleLoaders.LogTag);
            }

            Action<bool> callback = _onReady;
            _onReady = null;
            callback(ok);
        }

        private bool AllDone()
        {
            foreach (KeyValuePair<AbProviderKey, Item> pair in _assets)
            {
                if (!pair.Value.Done)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class AssetBundleCaches
    {
        public static AssetBundleHandle<T> Load<T>(string bundleName, string assetName, LoadPriority priority) where T : UnityEngine.Object
        {
            System.Type assetType = typeof(T);
            if (assetType == typeof(GameObject))
            {
                return (AssetBundleHandle<T>)(object)PrefabLoader.Instance.Load(bundleName, assetName, priority);
            }

            if (assetType == typeof(Sprite))
            {
                return (AssetBundleHandle<T>)(object)ImageLoader.Instance.LoadSprite(bundleName, assetName, priority);
            }

            if (assetType == typeof(Texture))
            {
                return (AssetBundleHandle<T>)(object)ImageLoader.Instance.LoadTexture(bundleName, assetName, priority);
            }

            if (assetType == typeof(AudioClip))
            {
                return (AssetBundleHandle<T>)(object)AudioLoader.Instance.Load(bundleName, assetName, priority);
            }

            if (assetType == typeof(TextAsset))
            {
                return (AssetBundleHandle<T>)(object)TextLoader.Instance.Load(bundleName, assetName, priority);
            }

            GameLogController.Error("没有对应的加载器: " + assetType.Name, AssetBundleLoaders.LogTag);
            AssetBundleHandle<T> missing = new AssetBundleHandle<T>();
            missing.Complete(null);
            return missing;
        }
    }
}
