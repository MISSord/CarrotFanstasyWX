using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    internal class SelectableScrollerBinder<TData, TCellView> : ScrollerBinderBase<TData>
        where TCellView : class, ISelectableCellView<TData>, new()
    {
        private SelectableScrollerList<TData, TCellView> owner;

        public void SetOwner(SelectableScrollerList<TData, TCellView> listOwner)
        {
            this.owner = listOwner;
        }

        protected override void BindCell(EnhancedScrollerCellView cell, TData data, int dataIndex)
        {
            if (this.owner == null)
            {
                return;
            }

            ScrollerCellShell shell = cell as ScrollerCellShell;
            if (shell == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[SelectableScrollerList] Cell 需为 {nameof(ScrollerCellShell)}，实际: {cell.GetType().Name}");
                return;
            }

            shell.Bind<TData, TCellView>(
                data,
                dataIndex,
                this.owner.CellContext,
                this.owner.IsIndexSelected,
                this.owner.HandleCellClick);
            this.owner.NotifyCellBound(dataIndex);
        }
    }
}
