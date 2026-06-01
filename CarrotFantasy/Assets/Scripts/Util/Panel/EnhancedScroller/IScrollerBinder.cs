using EnhancedUI.EnhancedScroller;

namespace CarrotFantasy
{
    /// <summary>
    /// 列表数据与 Cell 的绑定策略。行尺寸在 FlexScrollerController.SetBinder 的 getCellSize 委托中配置。
    /// </summary>
    public interface IScrollerBinder
    {
        int Count { get; }

        void Bind(EnhancedScrollerCellView cell, int dataIndex);
    }
}
