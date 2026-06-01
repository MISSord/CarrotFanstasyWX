using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    internal class SelectableScrollerBinder<TData> : ScrollerBinderBase<TData>
    {
        private SelectableScrollerList<TData> _owner;

        public void SetOwner(SelectableScrollerList<TData> owner)
        {
            _owner = owner;
        }

        protected override void BindCell(EnhancedScrollerCellView cell, TData data, int dataIndex)
        {
            if (_owner == null)
            {
                return;
            }

            if (cell is SelectableScrollerCellView<TData> selectableCell)
            {
                selectableCell.Bind(
                    data,
                    dataIndex,
                    _owner.IsIndexSelected,
                    _owner.HandleCellClick);
                _owner.NotifyCellBound(dataIndex);
                return;
            }

            UnityEngine.Debug.LogWarning(
                $"[SelectableScrollerList] Cell 需继承 {nameof(SelectableScrollerCellView<TData>)}，实际: {cell.GetType().Name}");
        }
    }
}
