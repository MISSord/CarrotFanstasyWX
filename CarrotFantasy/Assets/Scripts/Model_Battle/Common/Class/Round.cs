using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 波次刷怪配置：round → phases（无 group）。
    /// </summary>
    public class Round
    {
        public const string SpawnModeSequential = "sequential";
        public const string SpawnModeParallel = "parallel";

        /// <summary>阶段内一行配表（可多行共享同一 phaseId）。</summary>
        public class SpawnPhaseEntry
        {
            public int phaseId;
            /// <summary>该 phase 首次出现时，相对上一阶段结束的等待（秒）。</summary>
            public float phaseGap;
            public string spawnMode = SpawnModeSequential;
            /// <summary>逗号分隔怪物 ID，如 "1,1,2,3"。</summary>
            public string monsterIds;
            /// <summary>sequential：每只怪相对上一只的间隔；parallel：相对本段起点偏移。</summary>
            public float delay;
        }

        public class RoundInfo
        {
            public int roundIndex;
            /// <summary>本波开始相对上一波清场后的等待（秒）。</summary>
            public float waveGap;

            public List<SpawnPhaseEntry> phases;
        }
    }
}
