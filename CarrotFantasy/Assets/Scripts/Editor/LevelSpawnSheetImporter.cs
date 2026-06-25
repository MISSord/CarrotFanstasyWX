using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CarrotFantasy;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 ConfigTools/LevelSpawn 下 CSV 刷怪配表导入并写入关卡 JSON 的 roundInfo。
/// </summary>
public static class LevelSpawnSheetImporter
{
    private const string SpawnSheetRelativePath = "ConfigTools/LevelSpawn";
    private const string LevelJsonFolder = "Assets/Game/Json/Level";

    private static string GetRepositoryRoot()
    {
        var assetsDir = new DirectoryInfo(Application.dataPath);
        DirectoryInfo carrotFantasyDir = assetsDir.Parent;
        if (carrotFantasyDir == null)
        {
            throw new InvalidOperationException("无法解析工程根目录（Assets 上级目录不存在）");
        }

        DirectoryInfo repoRoot = carrotFantasyDir.Parent;
        if (repoRoot == null)
        {
            throw new InvalidOperationException("无法解析仓库根目录（CarrotFantasy 上级目录不存在）");
        }

        return repoRoot.FullName;
    }

    private static string GetSpawnSheetFolderAbsolute()
    {
        return Path.Combine(GetRepositoryRoot(), SpawnSheetRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    [MenuItem("CarrotFantasy/关卡刷怪表/导入 ConfigTools/LevelSpawn 下全部 CSV")]
    private static void ImportAllCsvInFolder()
    {
        string folder = GetSpawnSheetFolderAbsolute();
        if (!Directory.Exists(folder))
        {
            EditorUtility.DisplayDialog("导入刷怪表", "目录不存在: " + folder, "确定");
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.csv", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("导入刷怪表", "目录下没有 CSV: " + folder, "确定");
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            ImportCsvFile(files[i], syncMapConfig: false);
        }

        MapConfigWaveSync.SyncAllLevelsFromJson();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[LevelSpawnSheet] 已处理 CSV 数量: {0}（目录: {1}）", files.Length, folder));
    }

    [MenuItem("CarrotFantasy/关卡刷怪表/选择 CSV 文件导入...")]
    private static void ImportCsvViaFilePanel()
    {
        string folder = GetSpawnSheetFolderAbsolute();
        string path = EditorUtility.OpenFilePanel("选择关卡刷怪 CSV", folder, "csv");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        ImportCsvFile(path, syncMapConfig: true);
    }

    public static void ImportCsvFile(string csvPath, bool syncMapConfig = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(csvPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("CSV 不存在: " + fullPath);
            }

            List<LevelSpawnSheetRow> rows = ParseCsv(File.ReadAllText(fullPath, Encoding.UTF8));
            Dictionary<string, List<Round.RoundInfo>> byLevel = LevelSpawnSheetCompiler.CompileByLevel(rows);

            int written = 0;
            foreach (KeyValuePair<string, List<Round.RoundInfo>> kv in byLevel)
            {
                string[] parts = kv.Key.Split('_');
                int bigLevelId = int.Parse(parts[0]);
                int levelId = int.Parse(parts[1]);
                string fileName = LevelSpawnSheetCompiler.LevelFileName(bigLevelId, levelId);
                WriteRoundInfoToLevel(fileName, kv.Value);
                written++;
            }

            if (syncMapConfig)
            {
                MapConfigWaveSync.SyncFromRoundInfoByLevel(byLevel);
            }

            AssetDatabase.Refresh();
            Debug.Log(string.Format("[LevelSpawnSheet] 导入成功: {0}，写入关卡数: {1}", fullPath, written));
        }
        catch (Exception ex)
        {
            Debug.LogError(string.Format("[LevelSpawnSheet] 导入失败: {0}\n{1}", csvPath, ex));
            EditorUtility.DisplayDialog("导入刷怪表失败", ex.Message, "确定");
        }
    }

    private static void WriteRoundInfoToLevel(string fileName, List<Round.RoundInfo> roundInfo)
    {
        WriteRoundInfoToPath(Path.Combine(LevelJsonFolder, fileName), roundInfo);
    }

    private static void WriteRoundInfoToPath(string assetRelativePath, List<Round.RoundInfo> roundInfo)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetRelativePath));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("关卡 JSON 不存在: " + assetRelativePath);
        }

        string json = File.ReadAllText(fullPath, Encoding.UTF8);
        LevelInfo levelInfo = JsonMapper.ToObject<LevelInfo>(json);
        if (levelInfo == null)
        {
            throw new InvalidOperationException("关卡 JSON 解析失败: " + assetRelativePath);
        }

        levelInfo.roundInfo = roundInfo;
        string output = JsonMapper.ToJson(levelInfo);
        File.WriteAllText(fullPath, output, Encoding.UTF8);
        Debug.Log(string.Format("[LevelSpawnSheet] 已更新 roundInfo: {0}（{1} 波）", assetRelativePath, roundInfo.Count));
    }

    public static List<LevelSpawnSheetRow> ParseCsv(string text)
    {
        var rows = new List<LevelSpawnSheetRow>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return rows;
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            throw new InvalidOperationException("CSV 至少需要表头与一行数据");
        }

        string[] headers = SplitCsvLine(lines[0]);
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            columnIndex[headers[i].Trim()] = i;
        }

        RequireColumn(columnIndex, "bigLevelId");
        RequireColumn(columnIndex, "levelId");
        RequireColumn(columnIndex, "roundIndex");
        RequireColumn(columnIndex, "monsterIds");

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            string[] cells = SplitCsvLine(line);
            var row = new LevelSpawnSheetRow
            {
                bigLevelId = ParseInt(GetCell(cells, columnIndex, "bigLevelId"), lineIndex, "bigLevelId"),
                levelId = ParseInt(GetCell(cells, columnIndex, "levelId"), lineIndex, "levelId"),
                roundIndex = ParseInt(GetCell(cells, columnIndex, "roundIndex"), lineIndex, "roundIndex"),
                waveGap = ParseFloat(GetCell(cells, columnIndex, "waveGap"), 0f),
                phaseId = ParseInt(GetCell(cells, columnIndex, "phaseId"), lineIndex, "phaseId", defaultValue: 1),
                phaseGap = ParseFloat(GetCell(cells, columnIndex, "phaseGap"), 0f),
                spawnMode = GetCell(cells, columnIndex, "spawnMode"),
                monsterIds = GetCell(cells, columnIndex, "monsterIds"),
                delay = ParseFloat(GetCell(cells, columnIndex, "delay"), 0f),
                comment = GetCell(cells, columnIndex, "comment")
            };

            MonsterIdsParser.Parse(row.monsterIds);
            rows.Add(row);
        }

        return rows;
    }

    private static void RequireColumn(Dictionary<string, int> columnIndex, string name)
    {
        if (!columnIndex.ContainsKey(name))
        {
            throw new InvalidOperationException("CSV 缺少列: " + name);
        }
    }

    private static string GetCell(string[] cells, Dictionary<string, int> columnIndex, string column)
    {
        if (!columnIndex.TryGetValue(column, out int index) || index >= cells.Length)
        {
            return string.Empty;
        }

        return cells[index].Trim();
    }

    private static int ParseInt(string value, int lineIndex, string column, int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out int result))
        {
            throw new FormatException(string.Format("第 {0} 行 {1} 不是整数: \"{2}\"", lineIndex + 1, column, value));
        }

        return result;
    }

    private static float ParseFloat(string value, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!float.TryParse(value, out float result))
        {
            throw new FormatException(string.Format("浮点解析失败: \"{0}\"", value));
        }

        return result;
    }

    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Length = 0;
                continue;
            }

            current.Append(c);
        }

        cells.Add(current.ToString());
        return cells.ToArray();
    }
}
