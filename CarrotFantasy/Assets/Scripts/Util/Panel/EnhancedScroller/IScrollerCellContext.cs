namespace CarrotFantasy
{
    /// <summary>
    /// 列表数据上下文：CellView 可按 index 读取当前列表与选中态。
    /// </summary>
    public interface IScrollerCellContext<TData>
    {
        int Count { get; }

        TData GetItem(int index);

        bool IsIndexSelected(int index);
    }
}
