using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 基于 <see cref="FlexScrollerController"/> 的可选中列表。
    /// </summary>
    public class SelectableScrollerList<TData>
    {
        private readonly FlexScrollerController _flexScroller;
        private readonly int _defaultSelectIndex;
        private readonly SelectableScrollerBinder<TData> _binder = new SelectableScrollerBinder<TData>();

        private int _pendingJumpSelectIndex = -1;
        private bool _pendingJumpSelectInvokeCallback = true;

        private Action<int, TData> _onSelected;

        public int SelectedIndex { get; private set; } = -1;

        public TData SelectedItem =>
            SelectedIndex >= 0 && SelectedIndex < _binder.Count ? _binder.Items[SelectedIndex] : default;

        public IReadOnlyList<TData> Items => _binder.Items;

        public SelectableScrollerList(GameObject nodeObj, int defaultSelectIndex = -1)
        {
            var flexScroller = nodeObj != null ? nodeObj.GetComponent<FlexScrollerController>() : null;
            _flexScroller = flexScroller ?? throw new ArgumentNullException(nameof(flexScroller));
            _defaultSelectIndex = defaultSelectIndex;
            _binder.SetOwner(this);
        }

        public SelectableScrollerList(FlexScrollerController flexScroller, int defaultSelectIndex = -1)
        {
            _flexScroller = flexScroller ?? throw new ArgumentNullException(nameof(flexScroller));
            _defaultSelectIndex = defaultSelectIndex;
            _binder.SetOwner(this);
        }

        /// <summary>
        /// 设置整表数据并刷新列表（不包含选中与行高，请另行调用 <see cref="SetSelectIndex"/> / <see cref="SetCellSizeGetter"/>）。
        /// </summary>
        public void SetItemsList(List<TData> items, bool reload = true)
        {
            _binder.SetItemsList(items);
            ClearPendingJumpSelect();
            ClampSelectedIndex();

            _flexScroller.SetBinder(_binder, reload: false);

            if (reload)
            {
                _flexScroller.Reload();
            }
        }

        /// <summary>
        /// 设置选中行。<paramref name="selectIndex"/> 为 -1 时使用构造时的 defaultSelectIndex。
        /// </summary>
        public void SetSelectIndex(int selectIndex = -1, bool invokeCallback = false, bool refreshVisible = true)
        {
            var index = selectIndex >= 0 ? selectIndex : _defaultSelectIndex;
            Select(index, invokeCallback, refreshVisible);
        }

        public void SetCellSizeGetter(Func<int, float> getCellSize)
        {
            _flexScroller.SetCellSizeGetter(getCellSize);
        }

        /// <summary>
        /// 设置选中回调。传 null 可清除。
        /// </summary>
        public void SetOnSelected(Action<int, TData> onSelected)
        {
            _onSelected = onSelected;
        }

        public void Reload() => _flexScroller.Reload();

        public void RefreshVisible() => _flexScroller.RefreshVisible();

        /// <summary>
        /// 跳转到指定行。若 <paramref name="selectOnArrive"/> 为 true：
        /// 目标行 Cell 经 GetCellView 绑定出现时触发选中；若动画结束前未绑定到，则在 jumpComplete 时补一次。
        /// </summary>
        public void JumpTo(
            int dataIndex,
            float tweenTime = 0f,
            bool selectOnArrive = true,
            bool invokeCallback = true)
        {
            if (dataIndex < 0 || dataIndex >= _binder.Count)
            {
                ClearPendingJumpSelect();
                return;
            }

            if (selectOnArrive)
            {
                _pendingJumpSelectIndex = dataIndex;
                _pendingJumpSelectInvokeCallback = invokeCallback;
            }
            else
            {
                ClearPendingJumpSelect();
            }

            _flexScroller.JumpTo(dataIndex, tweenTime, OnJumpComplete);
        }

        internal bool IsIndexSelected(int index) => index >= 0 && index == SelectedIndex;

        internal void NotifyCellBound(int dataIndex)
        {
            if (_pendingJumpSelectIndex < 0 || dataIndex != _pendingJumpSelectIndex)
            {
                return;
            }

            TryApplyJumpSelection(dataIndex);
        }

        public void Select(int index, bool invokeCallback = true, bool refreshVisible = false)
        {
            if (_binder.Count == 0)
            {
                SetSelection(-1, invokeCallback, refreshVisible);
                return;
            }

            if (index < 0 || index >= _binder.Count)
            {
                SetSelection(-1, invokeCallback, refreshVisible);
                return;
            }

            if (index == SelectedIndex)
            {
                if (refreshVisible)
                {
                    _flexScroller.RefreshVisible();
                }

                return;
            }

            SetSelection(index, invokeCallback, refreshVisible);
        }

        internal void HandleCellClick(int index)
        {
            ClearPendingJumpSelect();
            if (index >= 0 && index < _binder.Count && index == SelectedIndex)
            {
                // 已选中行再次点击：仍触发业务回调（例如打开详情），避免“点了没反应”
                _onSelected?.Invoke(index, _binder.Items[index]);
                return;
            }

            Select(index, invokeCallback: true, refreshVisible: false);
        }

        private void OnJumpComplete()
        {
            if (_pendingJumpSelectIndex < 0)
            {
                return;
            }

            TryApplyJumpSelection(_pendingJumpSelectIndex);
        }

        private void TryApplyJumpSelection(int dataIndex)
        {
            if (_pendingJumpSelectIndex < 0 || _pendingJumpSelectIndex != dataIndex)
            {
                return;
            }

            var invokeCallback = _pendingJumpSelectInvokeCallback;
            ClearPendingJumpSelect();
            Select(dataIndex, invokeCallback, refreshVisible: true);
        }

        private void ClearPendingJumpSelect()
        {
            _pendingJumpSelectIndex = -1;
        }

        private void ClampSelectedIndex()
        {
            if (SelectedIndex >= _binder.Count)
            {
                SelectedIndex = -1;
            }
        }

        private void SetSelection(int index, bool invokeCallback, bool refreshVisible)
        {
            var changed = SelectedIndex != index;
            SelectedIndex = index;

            if (refreshVisible)
            {
                _flexScroller.RefreshVisible();
            }

            if (invokeCallback && changed)
            {
                if (SelectedIndex >= 0)
                {
                    _onSelected?.Invoke(SelectedIndex, _binder.Items[SelectedIndex]);
                }
                else
                {
                    _onSelected?.Invoke(-1, default);
                }
            }
        }
    }
}
