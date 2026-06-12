using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 将配表行（大章节子表）编译为各小关的 <see cref="Round.RoundInfo"/> 列表。
    /// </summary>
    public sealed class LevelSpawnSheetRow
    {
        public int bigLevelId;
        public int levelId;
        public int roundIndex;
        public float waveGap;
        public int phaseId;
        public float phaseGap;
        public string spawnMode = Round.SpawnModeSequential;
        public string monsterIds;
        public float delay;
        public string comment;
    }

    public static class LevelSpawnSheetCompiler
    {
        public static Dictionary<string, List<Round.RoundInfo>> CompileByLevel(IEnumerable<LevelSpawnSheetRow> rows)
        {
            var result = new Dictionary<string, List<Round.RoundInfo>>();
            if (rows == null)
            {
                return result;
            }

            var levelOrder = new List<string>();
            var levelRows = new Dictionary<string, List<LevelSpawnSheetRow>>();
            foreach (LevelSpawnSheetRow row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                string key = LevelKey(row.bigLevelId, row.levelId);
                if (!levelRows.TryGetValue(key, out List<LevelSpawnSheetRow> list))
                {
                    list = new List<LevelSpawnSheetRow>();
                    levelRows.Add(key, list);
                    levelOrder.Add(key);
                }

                list.Add(row);
            }

            for (int i = 0; i < levelOrder.Count; i++)
            {
                string key = levelOrder[i];
                result[key] = CompileLevelRows(levelRows[key]);
            }

            return result;
        }

        public static List<Round.RoundInfo> CompileLevelRows(List<LevelSpawnSheetRow> rows)
        {
            var roundOrder = new List<int>();
            var roundRows = new Dictionary<int, List<LevelSpawnSheetRow>>();
            for (int i = 0; i < rows.Count; i++)
            {
                LevelSpawnSheetRow row = rows[i];
                if (!roundRows.TryGetValue(row.roundIndex, out List<LevelSpawnSheetRow> list))
                {
                    list = new List<LevelSpawnSheetRow>();
                    roundRows.Add(row.roundIndex, list);
                    roundOrder.Add(row.roundIndex);
                }

                list.Add(row);
            }

            var rounds = new List<Round.RoundInfo>();
            for (int r = 0; r < roundOrder.Count; r++)
            {
                int roundIndex = roundOrder[r];
                List<LevelSpawnSheetRow> roundGroup = roundRows[roundIndex];
                if (roundIndex <= 0)
                {
                    throw new InvalidOperationException(string.Format("roundIndex 必须 >= 1，当前: {0}", roundIndex));
                }

                var round = new Round.RoundInfo
                {
                    roundIndex = roundIndex,
                    phases = new List<Round.SpawnPhaseEntry>()
                };

                float waveGap = 0f;
                bool waveGapSet = false;
                foreach (LevelSpawnSheetRow row in roundGroup)
                {
                    if (!waveGapSet && row.waveGap > 0f)
                    {
                        waveGap = row.waveGap;
                        waveGapSet = true;
                    }

                    if (string.IsNullOrWhiteSpace(row.monsterIds))
                    {
                        continue;
                    }

                    round.phases.Add(new Round.SpawnPhaseEntry
                    {
                        phaseId = row.phaseId > 0 ? row.phaseId : 1,
                        phaseGap = row.phaseGap,
                        spawnMode = string.IsNullOrWhiteSpace(row.spawnMode) ? Round.SpawnModeSequential : row.spawnMode.Trim(),
                        monsterIds = row.monsterIds.Trim(),
                        delay = row.delay
                    });
                }

                round.waveGap = waveGap;
                if (round.phases.Count == 0)
                {
                    throw new InvalidOperationException(string.Format("roundIndex={0} 无有效 monsterIds", roundIndex));
                }

                rounds.Add(round);
            }

            return rounds;
        }

        public static string LevelKey(int bigLevelId, int levelId)
        {
            return string.Format("{0}_{1}", bigLevelId, levelId);
        }

        public static string LevelFileName(int bigLevelId, int levelId)
        {
            return string.Format("Level{0}_{1}.json", bigLevelId, levelId);
        }
    }
}
