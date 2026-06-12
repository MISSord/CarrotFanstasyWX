using System.Collections.Generic;
using System.IO;
using LitJson;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 小关总波次查询。以关卡 JSON 的 <see cref="LevelInfo.roundInfo"/> 为准，<see cref="Stage.mTotalRound"/> 仅作兜底。
    /// </summary>
    public static class LevelWaveQuery
    {
        private static readonly Dictionary<string, int> WaveCountCache = new Dictionary<string, int>();

        public static int GetTotalWaves(int bigLevel, int level)
        {
            if (bigLevel <= 0 || level <= 0)
            {
                return 0;
            }

            string key = LevelSpawnSheetCompiler.LevelKey(bigLevel, level);
            if (WaveCountCache.TryGetValue(key, out int cached))
            {
                return cached;
            }

            int count = GetTotalWaves(LoadLevelInfo(bigLevel, level));
            if (count <= 0)
            {
                Stage stage = MapConfigReader.GetSingleStage(bigLevel, level);
                count = stage != null ? stage.mTotalRound : 0;
            }

            WaveCountCache[key] = count;
            return count;
        }

        public static int GetTotalWaves(LevelInfo levelInfo)
        {
            if (levelInfo?.roundInfo == null || levelInfo.roundInfo.Count == 0)
            {
                return 0;
            }

            return levelInfo.roundInfo.Count;
        }

        public static LevelInfo LoadLevelInfo(int bigLevel, int level)
        {
            string fileName = LevelSpawnSheetCompiler.LevelFileName(bigLevel, level);
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "Json", "Level", fileName);
            if (File.Exists(streamingPath))
            {
                return ParseLevelJson(File.ReadAllText(streamingPath));
            }

#if UNITY_EDITOR
            string editorPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "Game", "Json", "Level", fileName));
            if (File.Exists(editorPath))
            {
                return ParseLevelJson(File.ReadAllText(editorPath));
            }
#endif
            return null;
        }

        public static void ClearCache()
        {
            WaveCountCache.Clear();
        }

        private static LevelInfo ParseLevelJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonMapper.ToObject<LevelInfo>(json);
        }
    }
}
