namespace CarrotFantasy
{
    public abstract class SelectableCellViewBase<TData> : ISelectableCellView<TData>
    {
        protected ScrollerCellShell Shell { get; private set; }

        protected IScrollerCellContext<TData> Context { get; private set; }

        protected int DataIndex { get; private set; } = -1;

        public void Attach(ScrollerCellShell shell, IScrollerCellContext<TData> context)
        {
            Shell = shell;
            Context = context;
            OnAttach();
        }

        /// <summary>Shell 首次绑定时调用，可缓存 UI 引用、注册内部按钮。</summary>
        protected virtual void OnAttach()
        {
        }

        public void OnBind(TData data, int dataIndex)
        {
            DataIndex = dataIndex;
            OnBind(data);
        }

        protected abstract void OnBind(TData data);

        protected TData GetData()
        {
            if (Context == null || DataIndex < 0 || DataIndex >= Context.Count)
            {
                return default;
            }

            return Context.GetItem(DataIndex);
        }

        public virtual void OnRefreshSelected(bool selected)
        {
        }

        public virtual void OnRefresh(int dataIndex)
        {
        }

        public virtual void OnRecycle(int dataIndex)
        {
        }
    }
}
