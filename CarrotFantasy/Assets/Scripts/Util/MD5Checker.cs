using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// MD5校验，校验下载的AB包是否完整，考虑在下载完后进行校验
/// </summary>
public class MD5Checker
{
    public static string ComputeFileMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public static bool VerifyFileMD5(string filePath, string expectedMD5)
    {
        if (!File.Exists(filePath)) return false;

        string actualMD5 = ComputeFileMD5(filePath);
        return string.Equals(actualMD5, expectedMD5, StringComparison.OrdinalIgnoreCase);
    }
}