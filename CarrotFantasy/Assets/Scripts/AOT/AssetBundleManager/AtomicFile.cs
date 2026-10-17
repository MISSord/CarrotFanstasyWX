using System.IO;

/// <summary>
/// 先写旁边的临时文件，校验通过后再替换正式路径。失败时正式文件保持原样。
/// </summary>
public static class AtomicFile
{
    public static void Replace(string stagedPath, string finalPath)
    {
        if (string.IsNullOrEmpty(stagedPath) || string.IsNullOrEmpty(finalPath) || !File.Exists(stagedPath))
        {
            throw new FileNotFoundException("待替换文件不存在", stagedPath);
        }

        string directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(finalPath))
        {
            File.Move(stagedPath, finalPath);
            return;
        }

        string backupPath = finalPath + ".bak";
        File.Replace(stagedPath, finalPath, backupPath);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }
}
