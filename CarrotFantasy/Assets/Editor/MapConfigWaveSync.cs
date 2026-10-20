using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CarrotFantasy;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 将关卡 JSON 的 roundInfo 波次数同步到 Luban <c>TbMapLevel.TotalRound</c>（MapLevelInfo.xlsx / MapLevel），并重新导出。
/// </summary>
public static class MapConfigWaveSync
{
    const string PythonScriptRelative = "ConfigTools/LevelSpawn/_sync_mapconfig_waves.py";

    [MenuItem("Tools/CarrotFantasy/关卡刷怪表/同步所有关卡波次到 TbMapLevel", false, 12)]
    public static void SyncAllLevelsFromJson()
    {
        try
        {
            RunSync();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MapConfigWaveSync] 同步失败: " + ex);
            EditorUtility.DisplayDialog("同步 TbMapLevel 波次失败", ex.Message, "确定");
        }
    }

    public static void SyncFromRoundInfoByLevel(Dictionary<string, List<Round.RoundInfo>> byLevel)
    {
        RunSync();
    }

    static void RunSync()
    {
        int updated = SyncMapLevelWorkbook();
        ExportLuban();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[MapConfigWaveSync] 已从关卡 JSON 同步 {0} 个小关波次到 TbMapLevel", updated));
    }

    static int SyncMapLevelWorkbook()
    {
        string repoRoot = GetRepositoryRoot();
        string script = Path.Combine(repoRoot, PythonScriptRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(script))
        {
            throw new FileNotFoundException("缺少波次同步脚本: " + script);
        }

        string output = RunProcess("python", Quote(script), repoRoot);
        return ParseUpdatedCount(output);
    }

    static void ExportLuban()
    {
        string repoRoot = GetRepositoryRoot();
        string configDir = Path.Combine(repoRoot, "ConfigTools", "Config");
        string lubanDll = Path.Combine(repoRoot, "ConfigTools", "Luban", "Luban.dll");
        if (!File.Exists(lubanDll))
        {
            throw new FileNotFoundException("缺少 Luban: " + lubanDll);
        }

        string csOut = Path.Combine(repoRoot, "CarrotFantasy", "Assets", "Scripts", "Config", "Luban");
        string jsonOut = Path.Combine(repoRoot, "CarrotFantasy", "Assets", "Game", "Config", "Luban");
        string luaCodeOut = Path.Combine(repoRoot, "CarrotFantasy", "Assets", "Game", "LuaScripts", "gen", "luban");
        string luaDataOut = Path.Combine(repoRoot, "CarrotFantasy", "Assets", "Game", "LuaScripts", "gen", "luban_data");
        string args = string.Format(
            "{0} -t client -c cs-simple-json -c lua-lua -d json -d lua --conf __root__.conf -x {1} -x {2} -x {3} -x {4}",
            Quote(lubanDll),
            Quote("cs-simple-json.outputCodeDir=" + csOut),
            Quote("json.outputDataDir=" + jsonOut),
            Quote("lua-lua.outputCodeDir=" + luaCodeOut),
            Quote("lua.outputDataDir=" + luaDataOut));
        RunProcess("dotnet", args, configDir);
    }

    static int ParseUpdatedCount(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return 0;
        }

        const string marker = "updated ";
        int last = output.LastIndexOf(marker, StringComparison.Ordinal);
        if (last < 0)
        {
            return 0;
        }

        int start = last + marker.Length;
        int end = start;
        while (end < output.Length && char.IsDigit(output[end]))
        {
            end++;
        }

        int value;
        if (int.TryParse(output.Substring(start, end - start), out value))
        {
            return value;
        }

        return 0;
    }

    static string RunProcess(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using (var process = Process.Start(psi))
        {
            if (process == null)
            {
                throw new InvalidOperationException("无法启动进程: " + fileName);
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.Format("{0} 失败 ({1}): {2}\n{3}", fileName, process.ExitCode, stdout, stderr));
            }

            if (!string.IsNullOrEmpty(stdout))
            {
                Debug.Log("[MapConfigWaveSync] " + stdout.Trim());
            }

            return stdout;
        }
    }

    static string Quote(string value)
    {
        return "\"" + value + "\"";
    }

    static string GetRepositoryRoot()
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
}
