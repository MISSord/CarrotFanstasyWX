using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    /// <summary>
    /// EnhancedScroller 通用 Cell 基类。子类实现 OnSetData 绑定 UI。
    /// </summary>
    public abstract class GenericScrollerCellView<TData> : EnhancedScrollerCellView
    {
        protected TData CachedData { get; private set; }

        public void SetData(TData data, int dataIndex)
        {
            CachedData = data;
            this.dataIndex = dataIndex;
            OnSetData(data, dataIndex);
        }

        protected abstract void OnSetData(TData data, int dataIndex);

        public override void RefreshCellView()
        {
            OnSetData(CachedData, dataIndex);
        }
    }
}
