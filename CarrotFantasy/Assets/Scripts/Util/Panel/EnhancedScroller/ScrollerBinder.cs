using System;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    /// <summary>
    /// 委托实现的 Binder，可用 lambda 快速配置，无需为每种数据写 Controller。
    /// </summary>
    public sealed class ScrollerBinder : IScrollerBinder
    {
        private readonly Func<int> _count;
        private readonly Action<EnhancedScrollerCellView, int> _bind;

        public ScrollerBinder(
            Func<int> count,
            Action<EnhancedScrollerCellView, int> bind)
        {
            _count = count ?? throw new ArgumentNullException(nameof(count));
            _bind = bind ?? throw new ArgumentNullException(nameof(bind));
        }

        public int Count => _count();

        public void Bind(EnhancedScrollerCellView cell, int dataIndex) => _bind(cell, dataIndex);

        public static ScrollerBinder Create(
            int count,
            Action<EnhancedScrollerCellView, int> bind)
        {
            return new ScrollerBinder(() => count, bind);
        }

        public static ScrollerBinder ForList<TData, TCell>(
            IList<TData> list,
            Action<TCell, TData, int> bindCell)
            where TCell : EnhancedScrollerCellView
        {
            return new ScrollerBinder(
                () => list?.Count ?? 0,
                (cell, index) =>
                {
                    if (list == null || index < 0 || index >= list.Count)
                    {
                        return;
                    }

                    var typedCell = cell as TCell;
                    if (typedCell != null)
                    {
                        bindCell(typedCell, list[index], index);
                    }
                });
        }
    }
}
