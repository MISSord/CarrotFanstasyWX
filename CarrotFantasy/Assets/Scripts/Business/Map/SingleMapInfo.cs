namespace CarrotFantasy
{
    /// <summary>
    /// 单个小关的存档数据（与 <see cref="MapModel.ParseMapInfo"/> 串里「单格」五元组顺序一致：<c>大关,小关,萝卜,清道具,解锁</c>）。
    /// 取值均在 0–255，字段用 <see cref="byte"/>；枚举含义见 <see cref="MapInfoType"/>。
    /// </summary>
    public class SingleMapInfo
    {
        /// <summary>大关卡 ID，当前配置为 1–3。</summary>
        public byte bigLevelId;

        /// <summary>该大关下的小关序号，当前为 1–5（每大关 5 小关）。</summary>
        public byte levelId;

        /// <summary>
        /// 萝卜奖杯/状态：0 无；<see cref="MapInfoType.CARROT_STATE_NORMAL"/> 普通；
        /// <see cref="MapInfoType.CARROT_STATE_SLIVER"/> 银；<see cref="MapInfoType.CARROT_STATE_GOLD"/> 金（与战斗结算 <c>CarrotTropyLevel</c> 一致）。
        /// </summary>
        public byte carrotState;

        /// <summary>
        /// 是否清空本关道具：<see cref="MapInfoType.ALL_CLEAR"/> 已清空；<see cref="MapInfoType.NOT_ALL_CLEAR"/> 未清空。
        /// </summary>
        public byte isAllClear;

        /// <summary>
        /// 本关是否可进入：<see cref="MapInfoType.UNLOCK_LEVEL"/> 已解锁；<see cref="MapInfoType.LOCK_LEVEL"/> 未解锁。
        /// </summary>
        public byte unLocked;
    }
}
