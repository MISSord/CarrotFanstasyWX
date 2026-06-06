using System;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.Serialization;

namespace CarrotFantasy
{
    /// <summary>
    /// 列表 Controller：一个 Controller 对应一个 Cell Prefab，通过 IScrollerBinder 适配不同数据。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnhancedScroller))]
    public class FlexScrollerController : MonoBehaviour, IEnhancedScrollerDelegate
    {
        private EnhancedScroller scroller;
        [SerializeField]
        private EnhancedScrollerCellView cellPrefab;

        public EnhancedScrollerCellView CellPrefab => cellPrefab;
        private float fallbackCellSize = 10f;

        private IScrollerBinder _binder;
        private Func<int, float> _getCellSize;

        private void Awake()
        {
            scroller = GetComponent<EnhancedScroller>();
            if (scroller != null && scroller.Delegate == null)
            {
                scroller.Delegate = this;
            }
        }

        public void SetCellPrefab(EnhancedScrollerCellView prefab)
        {
            cellPrefab = prefab;
        }

        /// <summary>
        /// 绑定列表策略并刷新。<paramref name="getCellSize"/> 为 null 时使用 Cell Prefab 尺寸。
        /// </summary>
        public void SetBinder(IScrollerBinder binder, bool reload = true)
        {
            _binder = binder;
            if (reload)
            {
                Reload();
            }
        }

        public void SetCellSizeGetter(Func<int, float> getCellSize)
        {
            _getCellSize = getCellSize;
        }

        public void Reload()
        {
            if (scroller == null)
            {
                return;
            }

            scroller.Delegate = this;
            scroller.ReloadData();
        }

        public void RefreshVisible() => scroller?.RefreshActiveCellViews();

        public void JumpTo(int dataIndex, float tweenTime = 0f, Action onComplete = null)
        {
            if (scroller == null)
            {
                return;
            }

            if (tweenTime <= 0f)
            {
                scroller.JumpToDataIndex(dataIndex, jumpComplete: onComplete);
                return;
            }

            scroller.JumpToDataIndex(
                dataIndex,
                tweenType: EnhancedScroller.TweenType.linear,
                tweenTime: tweenTime,
                jumpComplete: onComplete);
        }

        public int GetNumberOfCells(EnhancedScroller scroller) => _binder?.Count ?? 0;

        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            if (_getCellSize != null)
            {
                return _getCellSize(dataIndex);
            }

            return GetDefaultCellSize();
        }

        private float GetDefaultCellSize()
        {
            if (cellPrefab == null || this.scroller == null)
            {
                return fallbackCellSize;
            }

            var rectTransform = cellPrefab.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return fallbackCellSize;
            }

            var isVertical = scroller.scrollDirection == EnhancedScroller.ScrollDirectionEnum.Vertical;
            var size = isVertical ? rectTransform.rect.height : rectTransform.rect.width;

            if (size <= 0f)
            {
                var sizeDelta = rectTransform.sizeDelta;
                size = isVertical ? sizeDelta.y : sizeDelta.x;
            }

            return size > 0f ? size : fallbackCellSize;
        }

        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            var cell = scroller.GetCellView(cellPrefab);
            _binder?.Bind(cell, dataIndex);
            return cell;
        }
    }
}
