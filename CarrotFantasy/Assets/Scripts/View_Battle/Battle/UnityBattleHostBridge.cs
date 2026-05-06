namespace CarrotFantasy
{
    /// <summary>
    /// 默认宿主实现：Unity 客户端下的 UI 提示与地图存档。
    /// </summary>
    public sealed class UnityBattleHostBridge : IBattleHostBridge
    {
        public void ShowInsufficientGoldTip()
        {
            UIServer.Instance.ShowTip(LanguageUtil.Instance.GetString(1004));
        }

        public void SubmitVictoryMapProgress(SingleMapInfo info)
        {
            MapServer.Instance.SendSetSingleMapInfo(info);
        }
    }
}
