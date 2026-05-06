namespace CarrotFantasy
{
    /// <summary>
    /// 战斗逻辑层语义事件（回放、联机同步、无 UI）。监听方负责转为表现或 IO。
    /// </summary>
    public static class BattleCoreEvent
    {
        /// <summary>PVE 一局结束；载荷 <see cref="PveMatchSettlement"/>。</summary>
        public const string PVE_MATCH_SETTLED = "Core_PveMatchSettled";
    }
}
