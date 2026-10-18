using System.IO;
using System.Text;

/// <summary>
/// 先写旁边的临时文件，校验通过后再替换正式路径。失败时正式文件保持原样。
/// </summary>
/// <remarks>
/// 注意：仅靠 <see cref="File.Replace(string, string, string)"/> 只保证"改名"是原子的，
/// 不保证数据已真正落盘。掉电或进程被强杀时，NTFS 可能先持久化目录项、后持久化数据，
/// 结果就是"文件名正确、内容 0 字节"。因此暂存文件必须用本类的
/// <see cref="WriteAllBytes"/> / <see cref="WriteAllText"/> 写入——它们会 Flush(true) 落盘。
/// </remarks>
public static class AtomicFile
{
    /// <summary>写入暂存文件并强制落盘（fsync），随后可安全交给 <see cref="Replace"/>。</summary>
    public static void WriteAllBytes(string stagedPath, byte[] contents)
    {
        string directory = Path.GetDirectoryName(stagedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (FileStream stream = new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (contents != null && contents.Length > 0)
            {
                stream.Write(contents, 0, contents.Length);
            }

            stream.Flush(true);
        }
    }

    /// <summary>UTF-8 无 BOM，与原 File.WriteAllText 行为一致，但会强制落盘。</summary>
    public static void WriteAllText(string stagedPath, string contents)
    {
        WriteAllBytes(stagedPath, new UTF8Encoding(false).GetBytes(contents ?? string.Empty));
    }

    public static void Replace(string stagedPath, string finalPath)
    {
        if (string.IsNullOrEmpty(stagedPath) || string.IsNullOrEmpty(finalPath) || !File.Exists(stagedPath))
        {
            throw new FileNotFoundException("待替换文件不存在", stagedPath);
        }

        // 0 字节文件几乎一定是写入没落盘或被截断，宁可失败也不要覆盖掉好的旧文件。
        long stagedLength = new FileInfo(stagedPath).Length;
        if (stagedLength == 0)
        {
            throw new InvalidDataException("待替换文件长度为 0，已拒绝替换: " + stagedPath);
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
