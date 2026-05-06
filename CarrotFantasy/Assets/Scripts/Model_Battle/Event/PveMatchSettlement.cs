namespace CarrotFantasy
{
    /// <summary>PVE 结算内核数据：胜利时可携带待提交的关卡进度副本。</summary>
    public class PveMatchSettlement
    {
        public bool IsVictory;

        /// <summary>胜利时的关卡进度（已由逻辑层填好）；失败时为 null。</summary>
        public SingleMapInfo VictoryProgress;
    }
}
