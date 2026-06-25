using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CarrotFantasy;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将关卡 JSON 的 roundInfo 波次数同步到 MapConfig.mTotalRound。
/// </summary>
public static class MapConfigWaveSync
{
    private const string MapConfigGame = "Assets/Game/Json/MapConfig.json";
    private const string LevelJsonFolder = "Assets/Game/Json/Level";

    [MenuItem("CarrotFantasy/关卡刷怪表/同步所有关卡波次到 MapConfig")]
    public static void SyncAllLevelsFromJson()
    {
        try
        {
            var waveCounts = CollectWaveCountsFromLevelJson();
            int updated = ApplyWaveCounts(waveCounts);
            AssetDatabase.Refresh();
            Debug.Log(string.Format("[MapConfigWaveSync] 已从关卡 JSON 同步 {0} 个小关波次到 MapConfig", updated));
        }
        catch (Exception ex)
        {
            Debug.LogError("[MapConfigWaveSync] 同步失败: " + ex);
            EditorUtility.DisplayDialog("同步 MapConfig 波次失败", ex.Message, "确定");
        }
    }

    public static void SyncFromRoundInfoByLevel(Dictionary<string, List<Round.RoundInfo>> byLevel)
    {
        if (byLevel == null || byLevel.Count == 0)
        {
            return;
        }

        var waveCounts = new Dictionary<string, int>();
        foreach (KeyValuePair<string, List<Round.RoundInfo>> kv in byLevel)
        {
            waveCounts[kv.Key] = kv.Value != null ? kv.Value.Count : 0;
        }

        ApplyWaveCounts(waveCounts);
    }

    private static Dictionary<string, int> CollectWaveCountsFromLevelJson()
    {
        var result = new Dictionary<string, int>();
        string folder = Path.GetFullPath(LevelJsonFolder);
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException("关卡目录不存在: " + LevelJsonFolder);
        }

        string[] files = Directory.GetFiles(folder, "Level*.json");
        var pattern = new Regex(@"Level(\d+)_(\d+)\.json", RegexOptions.IgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            Match m = pattern.Match(name);
            if (!m.Success)
            {
                continue;
            }

            int big = int.Parse(m.Groups[1].Value);
            int level = int.Parse(m.Groups[2].Value);
            string json = File.ReadAllText(files[i], Encoding.UTF8);
            LevelInfo info = JsonMapper.ToObject<LevelInfo>(json);
            int count = LevelWaveQuery.GetTotalWaves(info);
            result[LevelSpawnSheetCompiler.LevelKey(big, level)] = count;
        }

        return result;
    }

    private static int ApplyWaveCounts(Dictionary<string, int> waveCounts)
    {
        return SyncMapConfigFile(MapConfigGame, waveCounts);
    }

    private static int SyncMapConfigFile(string assetRelativePath, Dictionary<string, int> waveCounts)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetRelativePath));
        if (!File.Exists(fullPath))
        {
            return 0;
        }

        string json = File.ReadAllText(fullPath, Encoding.UTF8);
        PlayerManager playerManager = JsonMapper.ToObject<PlayerManager>(json);
        if (playerManager?.unLockedNormalModelLevelList == null)
        {
            return 0;
        }

        int changed = 0;
        foreach (KeyValuePair<string, int> kv in waveCounts)
        {
            string[] parts = kv.Key.Split('_');
            int big = int.Parse(parts[0]);
            int level = int.Parse(parts[1]);
            int index = (big - 1) * 5 + (level - 1);
            if (index < 0 || index >= playerManager.unLockedNormalModelLevelList.Count)
            {
                continue;
            }

            Stage stage = playerManager.unLockedNormalModelLevelList[index];
            if (stage.mTotalRound != kv.Value)
            {
                stage.mTotalRound = kv.Value;
                changed++;
            }
        }

        if (changed > 0)
        {
            File.WriteAllText(fullPath, JsonMapper.ToJson(playerManager), Encoding.UTF8);
            Debug.Log(string.Format("[MapConfigWaveSync] 已更新 {0}（{1} 项）", assetRelativePath, changed));
        }

        return changed;
    }
}
