using System;
using EnhancedUI.EnhancedScroller;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 支持选中态的列表 Cell 基类。Prefab 上需有 <see cref="Button"/>（可挂在根节点）。
    /// </summary>
    public abstract class SelectableScrollerCellView<TData> : GenericScrollerCellView<TData>
    {
        private Button clickButton;
        private int _boundIndex = -1;
        private Func<int, bool> _isIndexSelected;
        private Action<int> _onClick;

        protected virtual void Awake()
        {
            if (clickButton == null)
            {
                clickButton = GetComponent<Button>();
            }

            if (clickButton != null)
            {
                clickButton.onClick.AddListener(HandleClick);
            }
        }

        protected virtual void OnDestroy()
        {
            if (clickButton != null)
            {
                clickButton.onClick.RemoveListener(HandleClick);
            }
        }

        public void Bind(TData data, int dataIndex, Func<int, bool> isIndexSelected, Action<int> onClick)
        {
            _boundIndex = dataIndex;
            _isIndexSelected = isIndexSelected;
            _onClick = onClick;
            SetData(data, dataIndex);
            RefreshSelectedState();
        }

        public virtual void SetSelected(bool selected)
        {
        }

        public override void RefreshCellView()
        {
            base.RefreshCellView();
            RefreshSelectedState();
        }

        private void RefreshSelectedState()
        {
            if (_boundIndex < 0)
            {
                return;
            }

            SetSelected(_isIndexSelected != null && _isIndexSelected(_boundIndex));
        }

        private void HandleClick()
        {
            if (_boundIndex >= 0)
            {
                _onClick?.Invoke(_boundIndex);
            }
        }
    }
}
