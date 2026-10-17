using System;

namespace CarrotFantasy
{
    internal interface IAbWaiter
    {
        bool IsReleased { get; }

        /// <summary>每次取出或释放都会变。通知完成时用来认出已经回池、又被下一次 Load 取走的实例。</summary>
        int LeaseId { get; }

        void Complete(UnityEngine.Object asset);
    }

    /// <summary>
    /// 一份 AB 资源引用。Release 归还一次 Provider 计数，并把句柄放回对象池。
    /// 重复 Release 无操作。Release 之后不要再使用这个实例，它可能被下一次 Load 取走。
    /// 缓存命中时，Load 返回前 IsDone 已经为 true。
    /// </summary>
    public sealed class AssetBundleHandle<T> : IDisposable, IAbWaiter where T : UnityEngine.Object
    {
        private const string LogTag = "AssetBundleLoader";
        private static readonly ObjectPool<AssetBundleHandle<T>> Pool =
            new ObjectPool<AssetBundleHandle<T>>(16, 256, Reset);

        internal AbAssetProvider Provider;
        private Action<T> _completed;
        private bool _done;
        private bool _succeeded;
        private bool _released;
        private bool _pooled;
        private bool _warned;
        private int _lease;

        /// <summary>从池里取一个空白句柄。Load 以外不要自己 new。</summary>
        internal static AssetBundleHandle<T> Rent()
        {
            AssetBundleHandle<T> handle = Pool.Get();
            handle._pooled = false;
            handle._released = false;
            handle._done = false;
            handle._succeeded = false;
            handle._warned = false;
            handle._completed = null;
            handle.Provider = null;
            return handle;
        }

        /// <summary>放回池之前断开资源、回调和完成状态，避免下次取出时还指着上一份引用。</summary>
        private static void Reset(AssetBundleHandle<T> handle)
        {
            handle.Provider = null;
            handle._completed = null;
            handle._done = false;
            handle._succeeded = false;
            handle._released = false;
            handle._warned = false;
            handle._pooled = true;
        }

        public bool IsValid => !_released && !_pooled;

        public bool IsDone => _done && !_pooled;

        public bool Succeeded => _succeeded && IsValid;

        public T Asset
        {
            get
            {
                if (_released || _pooled)
                {
                    if (!_warned)
                    {
                        _warned = true;
                        GameLogController.Warning("句柄已释放，读取返回 null", LogTag);
                    }

                    return null;
                }

                if (!_succeeded || Provider == null)
                {
                    return null;
                }

                return Provider.Asset as T;
            }
        }

        /// <summary>
        /// 结束时调用一次。订阅时若已经结束，立刻调用。
        /// 失败或取消时参数为 null。
        /// </summary>
        public event Action<T> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (_pooled)
                {
                    value(null);
                    return;
                }

                if (_done)
                {
                    value(_released || !_succeeded ? null : Provider.Asset as T);
                    return;
                }

                _completed += value;
            }
            remove { _completed -= value; }
        }

        bool IAbWaiter.IsReleased => _released || _pooled;

        int IAbWaiter.LeaseId => _lease;

        internal void Complete(UnityEngine.Object asset)
        {
            if (_done || _released || _pooled)
            {
                return;
            }

            _done = true;
            _succeeded = asset is T;
            Action<T> callback = _completed;
            _completed = null;
            callback?.Invoke(_succeeded ? (T)asset : null);
        }

        void IAbWaiter.Complete(UnityEngine.Object asset)
        {
            Complete(asset);
        }

        public void Release()
        {
            if (_released || _pooled)
            {
                return;
            }

            _released = true;
            _lease++;
            AbAssetProvider provider = Provider;
            Provider = null;
            if (provider != null && provider.Host != null)
            {
                provider.Host.NotifyHandleReleased(this, provider);
            }

            if (!_done)
            {
                Complete(null);
            }

            Pool.Release(this);
        }

        public void Dispose()
        {
            Release();
        }
    }
}
