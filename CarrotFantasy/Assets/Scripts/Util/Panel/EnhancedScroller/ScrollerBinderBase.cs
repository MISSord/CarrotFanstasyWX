using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    /// <summary>
    /// 可复用的 Binder 基类：每种业务数据继承此类即可（约十行），场景里仍用同一个 FlexScrollerController。
    /// </summary>
    public abstract class ScrollerBinderBase<TData> : IScrollerBinder
    {
        public List<TData> Items { get; } = new List<TData>();

        public int Count => Items.Count;

        /// <summary>
        /// 绑定外部整表数据（清空内部列表后写入）。
        /// </summary>
        public void SetItemsList(List<TData> items)
        {
            Items.Clear();
            if (items != null)
            {
                Items.AddRange(items);
            }
        }

        public void Bind(EnhancedScrollerCellView cell, int dataIndex)
        {
            if (dataIndex < 0 || dataIndex >= Items.Count)
            {
                return;
            }

            BindCell(cell, Items[dataIndex], dataIndex);
        }

        protected abstract void BindCell(EnhancedScrollerCellView cell, TData data, int dataIndex);
    }
}
