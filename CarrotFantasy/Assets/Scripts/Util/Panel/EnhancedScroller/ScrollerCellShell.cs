using System;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 通用 Cell 壳：Prefab 只挂本组件 + <see cref="UINameTable"/>，业务由 per-Shell 的 <see cref="ISelectableCellView{TData}"/> 驱动。
    /// </summary>
    public class ScrollerCellShell : EnhancedScrollerCellView
    {
        sealed class CellViewSlot
        {
            public object View;
            public Action<int> OnRefresh;
            public Action<bool> OnRefreshSelected;
            public Action<int> OnRecycle;
        }

        UINameTableDic nameTableDic;
        int boundIndex = -1;
        CellViewSlot cellViewSlot;
        Func<int, bool> isIndexSelected;
        Action<int> onClick;

        public int DataIndex => boundIndex;

        public UINameTableDic NameTable
        {
            get
            {
                EnsureNameTable();
                return nameTableDic;
            }
        }

        protected virtual void Awake()
        {
            EnsureNameTable();
            ResolveClickButton();
        }

        protected virtual void OnDestroy()
        {
            ReleaseClickButton();
            ReleaseNameTable();
            cellViewSlot = null;
        }

        protected virtual void OnDisable()
        {
            if (boundIndex < 0)
            {
                return;
            }

            cellViewSlot?.OnRecycle?.Invoke(boundIndex);
            boundIndex = -1;
        }

        public void Bind<TData, TCellView>(
            TData data,
            int dataIndex,
            IScrollerCellContext<TData> context,
            Func<int, bool> isSelected,
            Action<int> onCellClick)
            where TCellView : class, ISelectableCellView<TData>, new()
        {
            boundIndex = dataIndex;
            isIndexSelected = isSelected;
            onClick = onCellClick;

            TCellView cellView = EnsureCellView<TData, TCellView>(context);
            cellView.OnBind(data, dataIndex);
            cellView.OnRefreshSelected(isSelected != null && isSelected(dataIndex));
        }

        public override void RefreshCellView()
        {
            base.RefreshCellView();

            if (boundIndex < 0)
            {
                return;
            }

            cellViewSlot?.OnRefresh?.Invoke(boundIndex);
            cellViewSlot?.OnRefreshSelected?.Invoke(isIndexSelected != null && isIndexSelected(boundIndex));
        }

        TCellView EnsureCellView<TData, TCellView>(IScrollerCellContext<TData> context)
            where TCellView : class, ISelectableCellView<TData>, new()
        {
            if (cellViewSlot?.View is TCellView existing)
            {
                return existing;
            }

            if (cellViewSlot != null)
            {
                Debug.LogWarning(
                    $"[{nameof(ScrollerCellShell)}] CellView 类型不匹配，将重建: {name}");
            }

            TCellView cellView = new TCellView();
            cellView.Attach(this, context);
            cellViewSlot = new CellViewSlot
            {
                View = cellView,
                OnRefresh = cellView.OnRefresh,
                OnRefreshSelected = cellView.OnRefreshSelected,
                OnRecycle = cellView.OnRecycle,
            };
            return cellView;
        }

        void EnsureNameTable()
        {
            if (nameTableDic != null)
            {
                return;
            }

            nameTableDic = new UINameTableDic();
            UINameTable nameTable = GetComponent<UINameTable>();
            if (nameTable == null)
            {
                Debug.LogWarning($"[{nameof(ScrollerCellShell)}] 未挂 UINameTable: {name}");
                return;
            }

            nameTableDic.AddUINameTable(nameTable.GetNameTableList());
        }

        void ReleaseNameTable()
        {
            nameTableDic?.ClearAllInfo();
            nameTableDic = null;
        }

        void ResolveClickButton()
        {
            Button clickButton = GetComponent<Button>();
            if (clickButton != null)
            {
                clickButton.onClick.AddListener(HandleClick);
            }
        }

        void ReleaseClickButton()
        {
            Button clickButton = GetComponent<Button>();
            if (clickButton != null)
            {
                clickButton.onClick.RemoveListener(HandleClick);
            }
        }

        void HandleClick()
        {
            if (boundIndex >= 0)
            {
                onClick?.Invoke(boundIndex);
            }
        }
    }
}
