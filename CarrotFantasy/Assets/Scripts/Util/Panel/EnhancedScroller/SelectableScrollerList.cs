using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 基于 <see cref="FlexScrollerController"/> 的可选中列表；<typeparamref name="TCellView"/> 在类型上显式声明 Cell 逻辑。
    /// </summary>
    public class SelectableScrollerList<TData, TCellView> where TCellView : class, ISelectableCellView<TData>, new()
    {
        sealed class ScrollerCellContext : IScrollerCellContext<TData>
        {
            readonly SelectableScrollerList<TData, TCellView> owner;

            public ScrollerCellContext(SelectableScrollerList<TData, TCellView> owner)
            {
                this.owner = owner;
            }

            public int Count => owner.binder.Count;

            public TData GetItem(int index) =>
                index >= 0 && index < Count ? owner.binder.Items[index] : default;

            public bool IsIndexSelected(int index) => owner.IsIndexSelected(index);
        }

        readonly FlexScrollerController flexScroller;
        readonly int defaultSelectIndex;
        readonly SelectableScrollerBinder<TData, TCellView> binder = new SelectableScrollerBinder<TData, TCellView>();
        readonly ScrollerCellContext cellContext;

        int pendingJumpSelectIndex = -1;
        bool pendingJumpSelectInvokeCallback = true;
        Action<int, TData> onSelected;

        public int SelectedIndex { get; private set; } = -1;

        public TData SelectedItem =>
            SelectedIndex >= 0 && SelectedIndex < binder.Count ? binder.Items[SelectedIndex] : default;

        public IReadOnlyList<TData> Items => binder.Items;

        internal IScrollerCellContext<TData> CellContext => cellContext;

        /// <param name="nodeObj">挂有 <see cref="FlexScrollerController"/> 的节点。</param>
        /// <param name="cellPrefab">可选；传入时覆盖 Controller 上序列化的 Cell Prefab。</param>
        public SelectableScrollerList(
            GameObject nodeObj,
            ScrollerCellShell cellPrefab = null,
            int defaultSelectIndex = -1)
            : this(ResolveFlexScroller(nodeObj), cellPrefab, defaultSelectIndex)
        {
        }

        public SelectableScrollerList(
            FlexScrollerController flexScroller,
            ScrollerCellShell cellPrefab = null,
            int defaultSelectIndex = -1)
        {
            this.flexScroller = flexScroller ?? throw new ArgumentNullException(nameof(flexScroller));
            this.defaultSelectIndex = defaultSelectIndex;
            this.cellContext = new ScrollerCellContext(this);

            if (cellPrefab != null)
            {
                this.flexScroller.SetCellPrefab(cellPrefab);
            }

            EnsureCellShellPrefab();
            this.binder.SetOwner(this);
        }

        static FlexScrollerController ResolveFlexScroller(GameObject nodeObj)
        {
            if (nodeObj == null)
            {
                throw new ArgumentNullException(nameof(nodeObj));
            }

            FlexScrollerController flex = nodeObj.GetComponent<FlexScrollerController>();
            if (flex == null)
            {
                throw new ArgumentException(
                    $"[{nameof(SelectableScrollerList<TData, TCellView>)}] 缺少 {nameof(FlexScrollerController)}: {nodeObj.name}",
                    nameof(nodeObj));
            }

            return flex;
        }

        void EnsureCellShellPrefab()
        {
            if (this.flexScroller.CellPrefab is ScrollerCellShell)
            {
                return;
            }

            throw new InvalidOperationException(
                $"[{nameof(SelectableScrollerList<TData, TCellView>)}] Cell Prefab 须为 {nameof(ScrollerCellShell)}，" +
                $"当前: {this.flexScroller.CellPrefab?.GetType().Name ?? "null"}。" +
                $"CellView={typeof(TCellView).Name}");
        }

        public void SetItemsList(List<TData> items, bool reload = true)
        {
            binder.SetItemsList(items);
            ClearPendingJumpSelect();
            ClampSelectedIndex();

            flexScroller.SetBinder(binder, reload: false);

            if (reload)
            {
                flexScroller.Reload();
            }
        }

        public void SetSelectIndex(int selectIndex = -1, bool invokeCallback = false, bool refreshVisible = true)
        {
            int index = selectIndex >= 0 ? selectIndex : defaultSelectIndex;
            Select(index, invokeCallback, refreshVisible);
        }

        public void SetCellSizeGetter(Func<int, float> getCellSize)
        {
            flexScroller.SetCellSizeGetter(getCellSize);
        }

        public void SetOnSelected(Action<int, TData> callback)
        {
            onSelected = callback;
        }

        public void Reload() => flexScroller.Reload();

        public void RefreshVisible() => flexScroller.RefreshVisible();

        public void JumpTo(int dataIndex, float tweenTime = 0f, bool selectOnArrive = true, bool invokeCallback = true)
        {
            if (dataIndex < 0 || dataIndex >= binder.Count)
            {
                ClearPendingJumpSelect();
                return;
            }

            if (selectOnArrive)
            {
                pendingJumpSelectIndex = dataIndex;
                pendingJumpSelectInvokeCallback = invokeCallback;
            }
            else
            {
                ClearPendingJumpSelect();
            }

            flexScroller.JumpTo(dataIndex, tweenTime, OnJumpComplete);
        }

        internal bool IsIndexSelected(int index) => index >= 0 && index == SelectedIndex;

        internal void NotifyCellBound(int dataIndex)
        {
            if (pendingJumpSelectIndex < 0 || dataIndex != pendingJumpSelectIndex)
            {
                return;
            }

            TryApplyJumpSelection(dataIndex);
        }

        public void Select(int index, bool invokeCallback = true, bool refreshVisible = false)
        {
            if (binder.Count == 0)
            {
                SetSelection(-1, invokeCallback, refreshVisible);
                return;
            }

            if (index < 0 || index >= binder.Count)
            {
                SetSelection(-1, invokeCallback, refreshVisible);
                return;
            }

            if (index == SelectedIndex)
            {
                if (refreshVisible)
                {
                    flexScroller.RefreshVisible();
                }

                return;
            }

            SetSelection(index, invokeCallback, refreshVisible);
        }

        internal void HandleCellClick(int index)
        {
            ClearPendingJumpSelect();
            if (index >= 0 && index < binder.Count && index == SelectedIndex)
            {
                onSelected?.Invoke(index, binder.Items[index]);
                return;
            }

            Select(index, invokeCallback: true, refreshVisible: false);
        }

        void OnJumpComplete()
        {
            if (pendingJumpSelectIndex < 0)
            {
                return;
            }

            TryApplyJumpSelection(pendingJumpSelectIndex);
        }

        void TryApplyJumpSelection(int dataIndex)
        {
            if (pendingJumpSelectIndex < 0 || pendingJumpSelectIndex != dataIndex)
            {
                return;
            }

            bool invokeCallback = pendingJumpSelectInvokeCallback;
            ClearPendingJumpSelect();
            Select(dataIndex, invokeCallback, refreshVisible: true);
        }

        void ClearPendingJumpSelect()
        {
            pendingJumpSelectIndex = -1;
        }

        void ClampSelectedIndex()
        {
            if (SelectedIndex >= binder.Count)
            {
                SelectedIndex = -1;
            }
        }

        void SetSelection(int index, bool invokeCallback, bool refreshVisible)
        {
            bool changed = SelectedIndex != index;
            SelectedIndex = index;

            if (refreshVisible || changed)
            {
                flexScroller.RefreshVisible();
            }

            if (invokeCallback && changed)
            {
                if (SelectedIndex >= 0)
                {
                    onSelected?.Invoke(SelectedIndex, binder.Items[SelectedIndex]);
                }
                else
                {
                    onSelected?.Invoke(-1, default);
                }
            }
        }
    }
}
