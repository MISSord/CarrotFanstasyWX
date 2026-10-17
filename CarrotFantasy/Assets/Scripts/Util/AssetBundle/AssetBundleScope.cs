using System;
using System.Collections.Generic;
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
        private bool _disposed;

        internal static readonly List<AssetBundleScope> All = new List<AssetBundleScope>();

        public string Name { get; }

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
                DeliverUse(new KeepRecord { Key = key, Done = true });
                return;
            }

            if (_keeps.TryGetValue(key, out KeepRecord existing))
            {
                if (existing.Done)
                {
                    DeliverUse(existing);
                }

                return;
            }

            KeepRecord record = new KeepRecord { Key = key };
            _keeps[key] = record;
            AssetBundleHandle<T> handle = AssetBundleCaches.Load<T>(bundleName, assetName, priority);
            record.Handle = handle;
            handle.Completed += asset => FinishKeep(record, asset);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UseWait[] pendingUses = _useWaiters.ToArray();
            _useWaiters.Clear();
            for (int i = 0; i < pendingUses.Length; i++)
            {
                pendingUses[i].Invoke(null);
            }

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
            All.Remove(this);
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
            if (record.Succeeded)
            {
                return;
            }

            _keeps.Remove(record.Key);
            record.Handle?.Dispose();
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
            AssetBundleHandle<T> missing = AssetBundleHandle<T>.Rent();
            missing.Complete(null);
            return missing;
        }
    }
}
