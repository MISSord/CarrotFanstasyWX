using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 轻量 C# 对象池（Stack）。用于频繁分配的短生命周期引用类型（如 HandleInfo / TokenInfo）。
    /// </summary>
    public sealed class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _stack;
        private readonly int _maxSize;
        private readonly Action<T> _onRelease;

        /// <param name="initialCapacity">Stack 初始容量。</param>
        /// <param name="maxSize">池中最多保留个数；超出则丢弃，交给 GC。</param>
        /// <param name="onRelease">归还前清理回调（断开引用，避免泄漏）。</param>
        public ObjectPool(int initialCapacity = 8, int maxSize = 256, Action<T> onRelease = null)
        {
            _stack = new Stack<T>(Math.Max(0, initialCapacity));
            _maxSize = maxSize > 0 ? maxSize : int.MaxValue;
            _onRelease = onRelease;
        }

        public int Count => _stack.Count;

        public T Get()
        {
            return _stack.Count > 0 ? _stack.Pop() : new T();
        }

        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            _onRelease?.Invoke(item);
            if (_stack.Count < _maxSize)
            {
                _stack.Push(item);
            }
        }

        public void Clear()
        {
            _stack.Clear();
        }
    }
}
