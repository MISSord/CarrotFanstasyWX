using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 打包完成后提示是否需要重打 PC：对比 AOT/Shared 与上次 Windows Player 打包指纹。
/// </summary>
public static class PcPlayerRebuildAdvisor
{
    /// <summary>工程内持久化（不放 Library，避免清理/重开后丢失）。</summary>
    const string ProjectFingerprintRelativePath = "ProjectSettings/CarrotFantasyPcAotFingerprint.json";

    /// <summary>打进/写在 PC 包旁的旁路文件，用于找回基线。</summary>
    const string BuildSidecarFileName = "CarrotFantasyPcAotFingerprint.json";

    const string BuildSettingsMenuPath = "File/Build Settings...";

    /// <summary>只监视会进 Player 的业务 AOT 源码；不含 HotUpdate，也不含 Generate 产物（避免仅热更代码误报）。</summary>
    static readonly string[] WatchedFolders =
    {
        "Assets/Scripts/AOT",
        "Assets/Scripts/Shared",
    };

    [Serializable]
    class FingerprintData
    {
        public string target;
        public string builtAtUtc;
        public string outputPath;
        public string fingerprint;
        public string[] folderNames;
        public string[] folderHashes;
    }

    public struct Assessment
    {
        public bool NeedsRebuild;
        public bool HasBaseline;
        public string Message;
        public string[] ChangedFolders;
    }

    [MenuItem("Tools/HybridCLR/标记当前 AOT 已与 PC 包同步", priority = 120)]
    public static void MarkSyncedFromMenu()
    {
        string path = RecordBaseline(BuildTarget.StandaloneWindows64, "菜单手动标记", null);
        EditorUtility.DisplayDialog(
            "已标记",
            "已按当前 AOT/Shared 写入指纹基线。\n\n" + path,
            "确定");
    }

    /// <summary>AB 打包或代码热更成功后调用。</summary>
    public static void ShowAfterPack(string packContext)
    {
        Assessment assessment = Assess();
        string title = "是否需要重新打包 PC";
        string body = string.IsNullOrEmpty(packContext)
            ? assessment.Message
            : packContext + "\n\n" + assessment.Message;

        if (assessment.NeedsRebuild)
        {
            bool openBuild = EditorUtility.DisplayDialog(
                title,
                body,
                "打开 Build 界面",
                "稍后再说");
            if (openBuild)
            {
                OpenBuildSettings();
            }

            return;
        }

        EditorUtility.DisplayDialog(title, body, "确定");
    }

    public static Assessment Assess()
    {
        TrySeedBaselineFromExistingPcBuild();

        Dictionary<string, string> currentFolders = ComputeFolderHashes();
        string currentFingerprint = CombineFingerprint(currentFolders);

        FingerprintData baseline = LoadBaseline();
        if (baseline == null || string.IsNullOrEmpty(baseline.fingerprint))
        {
            return new Assessment
            {
                NeedsRebuild = true,
                HasBaseline = false,
                ChangedFolders = Array.Empty<string>(),
                Message =
                    "尚未记录上次 PC（Windows）打包的 AOT 指纹。\n" +
                    "若改过 AOT / Shared，必须重打 PC；若不确定，建议打开 Build 界面打包。\n\n" +
                    "也可在菜单执行：Tools → HybridCLR → 标记当前 AOT 已与 PC 包同步\n" +
                    "说明：仅改 HotUpdate 通常不必重打 PC。",
            };
        }

        if (string.Equals(baseline.fingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            string builtAt = string.IsNullOrEmpty(baseline.builtAtUtc) ? "未知时间" : baseline.builtAtUtc;
            return new Assessment
            {
                NeedsRebuild = false,
                HasBaseline = true,
                ChangedFolders = Array.Empty<string>(),
                Message =
                    "对比上次 PC 打包记录：AOT / Shared 无变化。\n" +
                    "通常只需热更资源或 HotUpdate 代码，无需重打 PC 客户端。\n\n" +
                    "上次记录: " + builtAt + " (" + (baseline.target ?? "StandaloneWindows64") + ")",
            };
        }

        string[] changed = DiffFolders(baseline, currentFolders);
        var sb = new StringBuilder(256);
        sb.AppendLine("检测到 AOT/Shared 自上次 PC 打包后有变化，需要重新打包 PC 客户端。");
        sb.AppendLine("否则运行中的 exe 仍是旧 AOT，热更无法覆盖这些改动。");
        sb.AppendLine();
        if (changed.Length > 0)
        {
            sb.AppendLine("变化目录:");
            for (int i = 0; i < changed.Length; i++)
            {
                sb.Append("  - ");
                sb.AppendLine(changed[i]);
            }

            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(baseline.builtAtUtc))
        {
            sb.Append("上次 PC 打包记录: ");
            sb.AppendLine(baseline.builtAtUtc);
        }

        sb.Append("监视范围: Assets/Scripts/AOT、Assets/Scripts/Shared（不含 HotUpdate）。");

        return new Assessment
        {
            NeedsRebuild = true,
            HasBaseline = true,
            ChangedFolders = changed,
            Message = sb.ToString(),
        };
    }

    public static void OpenBuildSettings()
    {
        if (!EditorApplication.ExecuteMenuItem(BuildSettingsMenuPath))
        {
            Debug.LogWarning(
                "[PcPlayerRebuildAdvisor] 无法打开 Build Settings（菜单: " + BuildSettingsMenuPath + "）。" +
                "请手动打开 File → Build Settings。");
            EditorUtility.DisplayDialog(
                "打开失败",
                "无法自动打开 Build 界面，请手动打开：\nFile → Build Settings...",
                "确定");
        }
    }

    public static void RecordAfterPcPlayerBuild(BuildReport report)
    {
        if (report == null)
        {
            return;
        }

        BuildTarget target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows)
        {
            Debug.Log("[PcPlayerRebuildAdvisor] 跳过非 Windows Player 打包: " + target);
            return;
        }

        string outputPath = report.summary.outputPath ?? string.Empty;
        if (IsHybridClrTempBuild(outputPath))
        {
            Debug.Log("[PcPlayerRebuildAdvisor] 跳过 HybridCLR StripAOT 临时包: " + outputPath);
            return;
        }

        string saved = RecordBaseline(target, "PostprocessBuild", outputPath);
        Debug.Log("[PcPlayerRebuildAdvisor] PC 打包成功，已记录 AOT 指纹 → " + saved);
    }

    public static string RecordBaseline(BuildTarget target, string reason, string outputPath)
    {
        Dictionary<string, string> folders = ComputeFolderHashes();
        var data = new FingerprintData
        {
            target = target.ToString(),
            builtAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            outputPath = outputPath ?? string.Empty,
            fingerprint = CombineFingerprint(folders),
            folderNames = new string[folders.Count],
            folderHashes = new string[folders.Count],
        };

        int index = 0;
        foreach (KeyValuePair<string, string> pair in folders)
        {
            data.folderNames[index] = pair.Key;
            data.folderHashes[index] = pair.Value;
            index++;
        }

        string json = JsonUtility.ToJson(data, true);
        string projectPath = GetProjectFingerprintFullPath();
        WriteTextFile(projectPath, json);

        string sidecarPath = ResolveBuildSidecarPath(outputPath);
        if (string.IsNullOrEmpty(sidecarPath))
        {
            sidecarPath = GetDefaultPcBuildSidecarPath();
        }

        if (!string.IsNullOrEmpty(sidecarPath))
        {
            WriteTextFile(sidecarPath, json);
        }

        Debug.Log(
            "[PcPlayerRebuildAdvisor] 写入指纹 (" + reason + ")\n  project: " + projectPath +
            (string.IsNullOrEmpty(sidecarPath) ? string.Empty : "\n  sidecar: " + sidecarPath));

        return projectPath;
    }

    /// <summary>
    /// 若工程内无指纹，但 Build/PC 已存在且明显新于 AOT/Shared，则用当前源码建立基线（修复漏记）。
    /// </summary>
    static void TrySeedBaselineFromExistingPcBuild()
    {
        if (LoadBaseline() != null)
        {
            return;
        }

        string pcArtifact = FindNewestPcBuildArtifact();
        if (string.IsNullOrEmpty(pcArtifact) || !File.Exists(pcArtifact))
        {
            return;
        }

        DateTime pcTime = File.GetLastWriteTimeUtc(pcArtifact);
        DateTime newestSource = GetNewestWatchedSourceWriteTimeUtc();
        if (newestSource > pcTime)
        {
            Debug.Log(
                "[PcPlayerRebuildAdvisor] Build/PC 存在但早于 AOT/Shared 修改，不自动建基线。 pc=" +
                pcTime + " source=" + newestSource);
            return;
        }

        string sidecar = Path.Combine(Path.GetDirectoryName(pcArtifact) ?? string.Empty, BuildSidecarFileName);
        RecordBaseline(BuildTarget.StandaloneWindows64, "SeedFromExistingPcBuild", sidecar);
        Debug.LogWarning(
            "[PcPlayerRebuildAdvisor] 未找到工程指纹，但检测到较新的 PC 包，已按当前 AOT/Shared 自动建立基线。\n" +
            pcArtifact);
    }

    static bool IsHybridClrTempBuild(string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            return false;
        }

        string normalized = outputPath.Replace('\\', '/');
        return normalized.IndexOf("StrippedAOTDllsTempProj", StringComparison.OrdinalIgnoreCase) >= 0
               || normalized.IndexOf("HybridCLRData", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Dictionary<string, string> ComputeFolderHashes()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string projectRoot = GetProjectRoot();

        for (int i = 0; i < WatchedFolders.Length; i++)
        {
            string relative = WatchedFolders[i].Replace('\\', '/');
            string full = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            result[relative] = HashDirectory(full, projectRoot);
        }

        return result;
    }

    static string HashDirectory(string directory, string projectRoot)
    {
        if (!Directory.Exists(directory))
        {
            return "missing";
        }

        var files = new List<string>(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
        files.Sort(StringComparer.OrdinalIgnoreCase);

        using (var sha = SHA256.Create())
        {
            bool any = false;
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                if (!IsWatchedFile(file))
                {
                    continue;
                }

                any = true;
                string relative = ToProjectRelative(file, projectRoot);
                byte[] nameBytes = Encoding.UTF8.GetBytes(relative);
                sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);

                byte[] content = File.ReadAllBytes(file);
                sha.TransformBlock(content, 0, content.Length, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return any ? BytesToHex(sha.Hash) : "empty";
        }
    }

    static bool IsWatchedFile(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase)
               || string.Equals(ext, ".asmdef", StringComparison.OrdinalIgnoreCase);
    }

    static DateTime GetNewestWatchedSourceWriteTimeUtc()
    {
        DateTime newest = DateTime.MinValue;
        string projectRoot = GetProjectRoot();
        for (int i = 0; i < WatchedFolders.Length; i++)
        {
            string full = Path.Combine(
                projectRoot,
                WatchedFolders[i].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
            {
                continue;
            }

            string[] files = Directory.GetFiles(full, "*", SearchOption.AllDirectories);
            for (int f = 0; f < files.Length; f++)
            {
                if (!IsWatchedFile(files[f]))
                {
                    continue;
                }

                DateTime t = File.GetLastWriteTimeUtc(files[f]);
                if (t > newest)
                {
                    newest = t;
                }
            }
        }

        return newest;
    }

    static string CombineFingerprint(Dictionary<string, string> folderHashes)
    {
        var sb = new StringBuilder(256);
        var keys = new List<string>(folderHashes.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keys.Count; i++)
        {
            sb.Append(keys[i]);
            sb.Append('=');
            sb.Append(folderHashes[keys[i]]);
            sb.Append(';');
        }

        using (var sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return BytesToHex(sha.ComputeHash(bytes));
        }
    }

    static string[] DiffFolders(FingerprintData baseline, Dictionary<string, string> current)
    {
        var changed = new List<string>();
        var baselineMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (baseline.folderNames != null && baseline.folderHashes != null)
        {
            int count = Math.Min(baseline.folderNames.Length, baseline.folderHashes.Length);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(baseline.folderNames[i]))
                {
                    baselineMap[baseline.folderNames[i]] = baseline.folderHashes[i] ?? string.Empty;
                }
            }
        }

        foreach (KeyValuePair<string, string> pair in current)
        {
            string oldHash;
            if (!baselineMap.TryGetValue(pair.Key, out oldHash)
                || !string.Equals(oldHash, pair.Value, StringComparison.Ordinal))
            {
                changed.Add(pair.Key);
            }
        }

        return changed.ToArray();
    }

    static FingerprintData LoadBaseline()
    {
        FingerprintData fromProject = TryReadFingerprintFile(GetProjectFingerprintFullPath());
        if (fromProject != null)
        {
            return fromProject;
        }

        string sidecar = GetDefaultPcBuildSidecarPath();
        return TryReadFingerprintFile(sidecar);
    }

    static FingerprintData TryReadFingerprintFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            FingerprintData data = JsonUtility.FromJson<FingerprintData>(json);
            if (data == null || string.IsNullOrEmpty(data.fingerprint))
            {
                return null;
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PcPlayerRebuildAdvisor] 读取指纹失败: " + path + " | " + e.Message);
            return null;
        }
    }

    static string GetProjectFingerprintFullPath()
    {
        return Path.GetFullPath(Path.Combine(GetProjectRoot(), ProjectFingerprintRelativePath));
    }

    static string GetDefaultPcBuildSidecarPath()
    {
        string pcDir = Path.Combine(GetProjectRoot(), "Build", "PC");
        if (!Directory.Exists(pcDir))
        {
            return null;
        }

        return Path.Combine(pcDir, BuildSidecarFileName);
    }

    static string ResolveBuildSidecarPath(string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            return null;
        }

        try
        {
            string full = Path.GetFullPath(outputPath);
            string dir = File.Exists(full) ? Path.GetDirectoryName(full) : full;
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            return Path.Combine(dir, BuildSidecarFileName);
        }
        catch
        {
            return null;
        }
    }

    static string FindNewestPcBuildArtifact()
    {
        string pcDir = Path.Combine(GetProjectRoot(), "Build", "PC");
        if (!Directory.Exists(pcDir))
        {
            return null;
        }

        string[] candidates =
        {
            Path.Combine(pcDir, "GameAssembly.dll"),
            Path.Combine(pcDir, PlayerSettings.productName + ".exe"),
            Path.Combine(pcDir, "Unity.exe"),
        };

        string best = null;
        DateTime bestTime = DateTime.MinValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
            {
                continue;
            }

            DateTime t = File.GetLastWriteTimeUtc(candidates[i]);
            if (t > bestTime)
            {
                bestTime = t;
                best = candidates[i];
            }
        }

        return best;
    }

    static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    static string ToProjectRelative(string file, string projectRoot)
    {
        string relative = file;
        if (relative.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return relative.Replace('\\', '/').ToLowerInvariant();
    }

    static void WriteTextFile(string path, string content)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content, Encoding.UTF8);
    }

    static string BytesToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
        }

        return sb.ToString();
    }
}

/// <summary>PC Player 打包成功后写入 AOT 指纹基线。</summary>
public sealed class PcPlayerAotFingerprintRecorder : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 10000; }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        try
        {
            if (report == null)
            {
                Debug.LogWarning("[PcPlayerRebuildAdvisor] OnPostprocessBuild report=null");
                return;
            }

            Debug.Log(
                "[PcPlayerRebuildAdvisor] OnPostprocessBuild result=" + report.summary.result +
                " platform=" + report.summary.platform +
                " output=" + report.summary.outputPath);

            if (report.summary.result != BuildResult.Succeeded)
            {
                return;
            }

            PcPlayerRebuildAdvisor.RecordAfterPcPlayerBuild(report);
        }
        catch (Exception e)
        {
            Debug.LogError("[PcPlayerRebuildAdvisor] OnPostprocessBuild 异常: " + e);
        }
    }
}
