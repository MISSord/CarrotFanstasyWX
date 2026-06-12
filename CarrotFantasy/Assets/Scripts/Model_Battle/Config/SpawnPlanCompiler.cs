using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 将 <see cref="Round.RoundInfo"/> 编译为怪物出场顺序与相对本波起点的时间偏移（秒）。
    /// </summary>
    public sealed class WaveSpawnPlan
    {
        public List<int> MonsterIds = new List<int>();
        public List<float> SpawnOffsets = new List<float>();

        public int Count => MonsterIds.Count;
    }

    public static class SpawnPlanCompiler
    {
        public static WaveSpawnPlan Compile(Round.RoundInfo round)
        {
            if (round == null || round.phases == null || round.phases.Count == 0)
            {
                return new WaveSpawnPlan();
            }

            return CompilePhases(round);
        }

        private static WaveSpawnPlan CompilePhases(Round.RoundInfo round)
        {
            var plan = new WaveSpawnPlan();
            float t = round.waveGap > 0f ? round.waveGap : 0f;
            var seenPhaseIds = new HashSet<int>();

            for (int i = 0; i < round.phases.Count; i++)
            {
                Round.SpawnPhaseEntry entry = round.phases[i];
                if (entry == null)
                {
                    continue;
                }

                if (!seenPhaseIds.Contains(entry.phaseId))
                {
                    if (entry.phaseGap > 0f)
                    {
                        t += entry.phaseGap;
                    }

                    seenPhaseIds.Add(entry.phaseId);
                }

                List<int> ids = MonsterIdsParser.Parse(entry.monsterIds);
                if (ids.Count == 0)
                {
                    continue;
                }

                bool parallel = IsParallel(entry.spawnMode);
                if (parallel)
                {
                    float parallelBase = t + entry.delay;
                    for (int m = 0; m < ids.Count; m++)
                    {
                        plan.MonsterIds.Add(ids[m]);
                        plan.SpawnOffsets.Add(parallelBase);
                    }
                }
                else
                {
                    for (int m = 0; m < ids.Count; m++)
                    {
                        if (m > 0)
                        {
                            t += entry.delay;
                        }

                        plan.MonsterIds.Add(ids[m]);
                        plan.SpawnOffsets.Add(t);
                    }
                }
            }

            return plan;
        }

        private static bool IsParallel(string spawnMode)
        {
            return string.Equals(spawnMode, Round.SpawnModeParallel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
