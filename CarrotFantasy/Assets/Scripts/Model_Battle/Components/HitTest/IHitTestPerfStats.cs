namespace CarrotFantasy
{
    /// <summary>碰撞组件性能采样（供 <see cref="BattleHitTestBenchmarkStatsComponent"/> 读取）。</summary>
    public interface IHitTestPerfStats
    {
        string ModeName { get; }

        /// <summary>上一逻辑帧碰撞阶段耗时（Stopwatch Ticks）。</summary>
        long LastTickElapsedTicks { get; }

        /// <summary>上一逻辑帧窄相位（圆-圆精确检测）次数。</summary>
        int LastNarrowPhaseCount { get; }

        /// <summary>上一逻辑帧粗检测候选对数量（网格版）；暴力版为全量配对检测次数。</summary>
        int LastBroadPhasePairCount { get; }

        long AccumulatedElapsedTicks { get; }

        int SampleFrameCount { get; }

        void ResetAccumulators();
    }
}
