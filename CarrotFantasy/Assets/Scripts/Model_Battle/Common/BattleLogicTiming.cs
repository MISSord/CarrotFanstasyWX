namespace CarrotFantasy
{
    /// <summary>
    /// 战斗逻辑帧节拍（固定步长）。修改 <see cref="LogicFramesPerSecond"/> 即可调试不同逻辑帧率。
    /// </summary>
    public static class BattleLogicTiming
    {
        /// <summary>逻辑仿真帧率（Hz）。例如 10 / 20 / 30 / 60。</summary>
        public static int LogicFramesPerSecond = 30;

        /// <summary>
        /// 单次渲染帧内最多执行的逻辑步数，防止长时间卡帧后出现「追赶螺旋」一次性模拟过久。
        /// </summary>
        public static int MaxLogicStepsPerRenderFrame = 8;

        /// <summary>单逻辑帧时长 Δt（秒）。由当前帧率推导。</summary>
        public static Fix64 LogicDeltaTime
        {
            get
            {
                int fps = LogicFramesPerSecond <= 0 ? 30 : LogicFramesPerSecond;
                return new Fix64(1f / fps);
            }
        }
    }
}
