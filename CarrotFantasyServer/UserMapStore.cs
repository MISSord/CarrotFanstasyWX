using System.Globalization;
using System.Text;

namespace CarrotFantasyServer;

/// <summary>用户地图快照持久化（与客户端 MapModel.ParseMapInfo / MapInfoHelper 格式一致）。</summary>
internal static class UserMapStore
{
    private const int MaxBigLevel = 3;
    private const int LevelsPerBig = 5;
    private const int UnlockPlayable = 1;
    private const int LockPlayable = 2;

    private static readonly object FileLock = new();

    /// <summary>与 MapInfoHelper.GetInitMapInfo 一致。</summary>
    internal static string DefaultMapSnapshot()
    {
        return string.Concat(
            "#1,1,0,2,1", "#1,2,0,2,2", "#1,3,0,2,2", "#1,4,0,2,2", "#1,5,0,2,2",
            "#2,1,0,2,1", "#2,2,0,2,2", "#2,3,0,2,2", "#2,4,0,2,2", "#2,5,0,2,2",
            "#3,1,0,2,1", "#3,2,0,2,2", "#3,3,0,2,2", "#3,4,0,2,2", "#3,5,0,2,2");
    }

    internal static string LoadOrCreate(long userId, string dataRoot)
    {
        string path = GetPath(userId, dataRoot);
        lock (FileLock)
        {
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path).Trim();
                if (IsWellFormedSnapshot(text))
                {
                    return text;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string def = DefaultMapSnapshot();
            File.WriteAllText(path, def);
            return def;
        }
    }

    internal static void Save(long userId, string dataRoot, string snapshot)
    {
        string path = GetPath(userId, dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        lock (FileLock)
        {
            File.WriteAllText(path, snapshot);
        }
    }

    /// <summary>写入当前关结算数据，并解锁下一关（若存在）。返回响应中的下一关坐标，无下一关时为 (0,0)。</summary>
    internal static (int nextBig, int nextSmall) ApplyVictoryAndSave(
        long userId,
        string dataRoot,
        int bigLevelId,
        int levelId,
        int carrotState,
        int isAllClear)
    {
        string snap = LoadOrCreate(userId, dataRoot);
        string[] parts = GetValidParts(snap);

        int idx = CellIndex(bigLevelId, levelId);
        parts[idx] = $"{bigLevelId},{levelId},{carrotState},{isAllClear},{UnlockPlayable}";

        (int nb, int ns) = NextLevel(bigLevelId, levelId);
        if (nb != 0)
        {
            int idx2 = CellIndex(nb, ns);
            (int c, int a, _) = ParseSegment(parts, idx2);
            parts[idx2] = $"{nb},{ns},{c},{a},{UnlockPlayable}";
        }

        string merged = BuildSnapshot(parts);
        Save(userId, dataRoot, merged);
        return (nb, ns);
    }

    private static string GetPath(long userId, string dataRoot) =>
        Path.Combine(dataRoot, "maps", userId + ".txt");

    private static bool IsWellFormedSnapshot(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] parts = text.Split('#');
        return parts.Length >= 16;
    }

    /// <summary>解析为 16 段（含索引 0 的空段）；格式非法时退回默认快照的段数组。</summary>
    private static string[] GetValidParts(string snapshot)
    {
        string[] parts = snapshot.Split('#');
        if (parts.Length >= 16)
        {
            return parts;
        }

        return DefaultMapSnapshot().Split('#');
    }

    private static string BuildSnapshot(string[] parts)
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= 15; i++)
        {
            sb.Append('#').Append(parts[i]);
        }

        return sb.ToString();
    }

    private static (int carrot, int clear, int unlocked) ParseSegment(string[] parts, int idx)
    {
        if (idx < 0 || idx >= parts.Length || string.IsNullOrEmpty(parts[idx]))
        {
            return (0, LockPlayable, LockPlayable);
        }

        string[] f = parts[idx].Split(',');
        if (f.Length < 5)
        {
            return (0, LockPlayable, LockPlayable);
        }

        if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int carrot)
            || !int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int clear)
            || !int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int unlocked))
        {
            return (0, LockPlayable, LockPlayable);
        }

        return (carrot, clear, unlocked);
    }

    private static int CellIndex(int big, int small) => (big - 1) * LevelsPerBig + small;

    private static (int big, int small) NextLevel(int big, int small)
    {
        if (small < LevelsPerBig)
        {
            return (big, small + 1);
        }

        if (big < MaxBigLevel)
        {
            return (big + 1, 1);
        }

        return (0, 0);
    }
}
