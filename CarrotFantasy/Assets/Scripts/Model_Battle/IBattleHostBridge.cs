namespace CarrotFantasy
{
    /// <summary>
    /// 战斗逻辑层与宿主环境（UI、存档、网络等）的桥接。Model 不直接依赖 UIServer/MapServer。
    /// </summary>
    public interface IBattleHostBridge
    {
        void ShowInsufficientGoldTip();

        void SubmitVictoryMapProgress(SingleMapInfo info);
    }
}
