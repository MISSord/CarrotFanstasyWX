using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 挂在 <see cref="HitTestBenchmarkBattle"/> 上，周期性输出碰撞性能采样（对比暴力版 / 网格版）。
    /// </summary>
    public class BattleHitTestBenchmarkStatsComponent : BaseBattleComponent
    {
        public const string ComponentTypeId = "HitTestBenchmarkStatsComponent";

        private IHitTestPerfStats perfSource;
        private int logIntervalFrames = 300;
        private int framesSinceLog;

        public BattleHitTestBenchmarkStatsComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = ComponentTypeId;
        }

        public override void Init()
        {
            BaseBattleComponent hitComp = this.baseBattle.GetComponent(BattleComponentType.HitTestComponent);
            this.perfSource = hitComp as IHitTestPerfStats;
            if (this.perfSource == null)
            {
                Debug.LogWarning("[HitTestBenchmark] 当前 HitTest 组件未实现 IHitTestPerfStats。");
            }

            if (BattleParamServer.Instance != null)
            {
                this.logIntervalFrames = BattleParamServer.Instance.hitTestBenchmarkLogIntervalFrames;
                if (this.logIntervalFrames <= 0)
                {
                    this.logIntervalFrames = 300;
                }
            }
        }

        public override void LateTick(Fix64 time)
        {
            if (this.perfSource == null)
            {
                return;
            }

            this.framesSinceLog += 1;
            if (this.framesSinceLog < this.logIntervalFrames)
            {
                return;
            }

            this.framesSinceLog = 0;
            this.LogSummary(false);
        }

        /// <summary>立即输出当前累计统计并重置累计器。</summary>
        public void LogSummary(bool resetAccumulators)
        {
            if (this.perfSource == null)
            {
                return;
            }

            int frames = this.perfSource.SampleFrameCount;
            if (frames <= 0)
            {
                return;
            }

            double avgMs = this.TicksToMs(this.perfSource.AccumulatedElapsedTicks) / frames;
            double lastMs = this.TicksToMs(this.perfSource.LastTickElapsedTicks);

            Debug.Log(string.Format(
                "[HitTestBenchmark] mode={0} frame={1} last={2:F3}ms narrow={3} broad={4} avg({5}f)={6:F3}ms",
                this.perfSource.ModeName,
                this.baseBattle.curFrameId,
                lastMs,
                this.perfSource.LastNarrowPhaseCount,
                this.perfSource.LastBroadPhasePairCount,
                frames,
                avgMs));

            if (resetAccumulators)
            {
                this.perfSource.ResetAccumulators();
            }
        }

        private double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public override void ClearInfo()
        {
            this.perfSource = null;
            this.framesSinceLog = 0;
        }
    }
}
