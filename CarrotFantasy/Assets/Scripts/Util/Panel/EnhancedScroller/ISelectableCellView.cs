namespace CarrotFantasy
{
    /// <summary>
    /// 可选中列表 Cell 逻辑（纯 C#，每个池化 ScrollerCellShell 对应一个实例）。
    /// </summary>
    public interface ISelectableCellView<TData>
    {
        /// <summary>绑定到 Shell 时调用一次（每个 ScrollerCellShell 一次）。</summary>
        void Attach(ScrollerCellShell shell, IScrollerCellContext<TData> context);

        /// <summary>绑定一行数据。</summary>
        void OnBind(TData data, int dataIndex);

        /// <summary>选中态变化。</summary>
        void OnRefreshSelected(bool selected);

        /// <summary><see cref="EnhancedUI.EnhancedScroller.EnhancedScroller.RefreshActiveCellViews"/> 时。</summary>
        void OnRefresh(int dataIndex);

        /// <summary>Cell 被回收隐藏时。</summary>
        void OnRecycle(int dataIndex);
    }
}
