using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public abstract class SelectableScrollerCellView<TData> : GenericScrollerCellView<TData>
    {
        protected UINameTableDic nameTableDic;
        private int _boundIndex = -1;
        private bool _isLoaded;
        private Func<int, bool> _isIndexSelected;
        private Action<int> _onClick;

        #region 生命周期（对齐 BaseView，映射 EnhancedScroller 复用）

        /// <summary>预制体实例首次就绪（Awake + 名称表初始化后），仅调用一次。</summary>
        protected virtual void LoadCallBack()
        {
        }

        /// <summary>实例销毁前。</summary>
        protected virtual void ReleaseCallBack()
        {
        }

        /// <summary>Cell 被回收隐藏时（OnDisable 且此前已绑定）。</summary>
        /// <param name="dataIndex">回收前绑定的数据行索引。</param>
        protected virtual void RecycleCallBack(int dataIndex)
        {
        }

        /// <summary><see cref="RefreshActiveCellViews"/> 刷新可见 Cell 时。</summary>
        protected virtual void RefreshCallBack()
        {
        }

        protected virtual void OnFlush(int dataIndex)
        {
        }

        #endregion

        protected virtual void Awake()
        {
            InitNameTable();
            ResolveClickButton();

            if (!_isLoaded)
            {
                _isLoaded = true;
                LoadCallBack();
            }
        }

        protected virtual void OnDestroy()
        {
            ReleaseCallBack();
            ReleaseClickButton();
            ReleaseNameTable();
            _isLoaded = false;
        }

        protected virtual void OnDisable()
        {
            if (_boundIndex < 0)
            {
                return;
            }

            RecycleCallBack(_boundIndex);
            _boundIndex = -1;
        }

        protected TData GetData()
        {
            return this.CachedData;
        }

        /// <summary>从本 Cell 预制体根上的 <see cref="UINameTable"/> 收集节点。</summary>
        protected void InitNameTable()
        {
            if (nameTableDic != null)
            {
                return;
            }

            nameTableDic = new UINameTableDic();
            UINameTable nameTable = GetComponent<UINameTable>();
            if (nameTable == null)
            {
                OnNameTableMissing();
                return;
            }

            nameTableDic.AddUINameTable(nameTable.GetNameTableList());
        }

        protected virtual void OnNameTableMissing()
        {
            Debug.LogWarning($"[{GetType().Name}] 未挂 UINameTable（可忽略）: {name}");
        }

        protected void ReleaseNameTable()
        {
            nameTableDic?.ClearAllInfo();
            nameTableDic = null;
        }

        protected virtual void ResolveClickButton()
        {
            Button clickButton = GetComponent<Button>();
            if (clickButton != null)
            {
                clickButton.onClick.AddListener(HandleClick);
            }
        }

        protected void ReleaseClickButton()
        {
            Button clickButton = GetComponent<Button>();
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
            ApplyPendingFlush(dataIndex);
            RefreshSelectedState();
        }

        public virtual void SetSelected(bool selected)
        {
        }

        public override void RefreshCellView()
        {
            base.RefreshCellView();
            RefreshSelectedState();
            RefreshCallBack();
        }

        private void ApplyPendingFlush(int dataIndex)
        {
            OnFlush(dataIndex);
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
