using System;
using UnityEngine;

namespace CarrotFantasy
{
    internal interface IAbWaiter
    {
        bool IsReleased { get; }

        void Complete(UnityEngine.Object asset);
    }

    /// <summary>
    /// 一份 AB 资源引用。Release 归还一次 Provider 计数，重复调用无操作。
    /// 缓存命中时，Load 返回前 IsDone 已经为 true。
    /// </summary>
    public sealed class AssetBundleHandle<T> : IDisposable, IAbWaiter where T : UnityEngine.Object
    {
        private const string LogTag = "AssetBundleLoader";

        internal AbAssetProvider Provider;
        private Action<T> _completed;
        private bool _done;
        private bool _succeeded;
        private bool _released;
        private bool _warned;

        public bool IsValid => !_released;

        public bool IsDone => _done;

        public bool Succeeded => _succeeded && !_released;

        public T Asset
        {
            get
            {
                if (_released)
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

                if (_done)
                {
                    value(_released || !_succeeded ? null : Provider.Asset as T);
                    return;
                }

                _completed += value;
            }
            remove { _completed -= value; }
        }

        bool IAbWaiter.IsReleased => _released;

        internal void Complete(UnityEngine.Object asset)
        {
            if (_done)
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
            if (_released)
            {
                return;
            }

            _released = true;
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
        }

        public void Dispose()
        {
            Release();
        }
    }
}
