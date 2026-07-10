using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>
/// 将下载完成的 Pack（ZIP）解压到本地 AB 目录，并校验每个 AB。
///
/// 校验层次：
/// 1. Pack 级：ZIP Size + MD5（VerifyPackFile）
/// 2. Entry 级：每个 BundleNames 对应文件的 Size + MD5（与清单 AB 信息比对）
/// 3. 落盘：直接拷贝，或 CompressedFormat==0 时 Recompress 为 LZ4 后再 LoadFromFile 验加载
///
/// 注意：当前未做 Zip Slip 路径穿越防护；entry.FullName 应始终为相对 BundleName。
/// Pack 内「本地已最新」的 AB 仍会覆盖写入，无跳过逻辑。
/// </summary>
public static class AssetBundlePackExtractor
{
    /// <summary>Pack 文件级校验：存在性、Size、MD5。</summary>
    public static bool VerifyPackFile(string zipPath, DownloadPackInfo packInfo, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (packInfo == null || string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
        {
            errorMessage = "Pack 文件不存在";
            return false;
        }

        FileInfo zipInfo = new FileInfo(zipPath);
        if (packInfo.PackSize > 0 && zipInfo.Length != packInfo.PackSize)
        {
            errorMessage = $"Pack 大小不匹配: 本地 {zipInfo.Length} != 清单 {packInfo.PackSize}";
            return false;
        }

        if (!string.IsNullOrEmpty(packInfo.PackHash)
            && !MD5Checker.VerifyFileMD5(zipPath, packInfo.PackHash))
        {
            errorMessage = "Pack MD5 不匹配";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 解压 Pack 并写入持久化 AB 目录。
    /// bundleInfoDict 仅含本次待更包时，Pack 内其它 entry 会跳过 Size/MD5（仍会落盘）。
    /// </summary>
    public static IEnumerator ExtractPackCoroutine(
        string zipPath,
        DownloadPackInfo packInfo,
        Dictionary<string, CustomAssetBundleInfo> bundleInfoDict,
        bool isNeedConvert,
        Action<bool, string> onComplete)
    {
        string errorMessage = string.Empty;
        if (!VerifyPackFile(zipPath, packInfo, out errorMessage))
        {
            onComplete?.Invoke(false, errorMessage);
            yield break;
        }

        if (packInfo.BundleNames == null || packInfo.BundleNames.Length == 0)
        {
            onComplete?.Invoke(false, "Pack 未包含任何 AB");
            yield break;
        }

        string extractRoot = Path.Combine(Application.temporaryCachePath, "pack_extract_" + packInfo.PackName);
        bool success = false;
        bool prepareFailed = false;

        try
        {
            PrepareExtractDirectory(extractRoot);
            ExtractZipToDirectory(zipPath, extractRoot);
        }
        catch (Exception e)
        {
            CleanupExtractDirectory(extractRoot);
            onComplete?.Invoke(false, $"Pack 解压失败: {e.Message}");
            prepareFailed = true;
        }

        if (prepareFailed)
        {
            yield break;
        }

        for (int i = 0; i < packInfo.BundleNames.Length; i++)
        {
            string bundleName = packInfo.BundleNames[i];
            string sourcePath = Path.Combine(extractRoot, bundleName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                errorMessage = $"Pack 内缺少 AB: {bundleName}";
                break;
            }

            // 仅当该 AB 在本次待更映射中时做 Size/MD5；顺带下载的其它包只检查存在性。
            if (bundleInfoDict != null
                && bundleInfoDict.TryGetValue(bundleName, out CustomAssetBundleInfo bundleInfo)
                && !VerifyBundleFile(sourcePath, bundleInfo))
            {
                errorMessage = $"Pack 内 AB 校验失败: {bundleName}";
                break;
            }

            string targetPath = AssetBundlePathHelper.GetLocalLZ4Path(bundleName);
            string targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (isNeedConvert)
            {
                // bundleName 含目录层级（如 ui/images/tower/5/xxx），必须先创建父目录。
                string convertTemp = Path.Combine(
                    Application.temporaryCachePath,
                    bundleName.Replace('/', Path.DirectorySeparatorChar) + ".pack.temp");
                string convertTempDir = Path.GetDirectoryName(convertTemp);
                if (!string.IsNullOrEmpty(convertTempDir) && !Directory.Exists(convertTempDir))
                {
                    Directory.CreateDirectory(convertTempDir);
                }

                try
                {
                    File.Copy(sourcePath, convertTemp, true);
                }
                catch (Exception e)
                {
                    errorMessage = $"Pack 内 AB 复制失败: {bundleName}, {e.Message}";
                    break;
                }

                bool convertOk = false;
                string convertError = string.Empty;
                yield return ConvertToLz4Coroutine(convertTemp, targetPath, (ok, err) =>
                {
                    convertOk = ok;
                    convertError = err;
                });

                if (!convertOk)
                {
                    errorMessage = string.IsNullOrEmpty(convertError)
                        ? $"Pack 内 AB 转换失败: {bundleName}"
                        : convertError;
                    break;
                }
            }
            else
            {
                try
                {
                    File.Copy(sourcePath, targetPath, true);
                }
                catch (Exception e)
                {
                    errorMessage = $"Pack 内 AB 写入失败: {bundleName}, {e.Message}";
                    break;
                }
            }
        }

        CleanupExtractDirectory(extractRoot);

        if (string.IsNullOrEmpty(errorMessage))
        {
            success = true;
        }

        onComplete?.Invoke(success, errorMessage);
    }

    static void PrepareExtractDirectory(string extractRoot)
    {
        if (Directory.Exists(extractRoot))
        {
            Directory.Delete(extractRoot, true);
        }

        Directory.CreateDirectory(extractRoot);
    }

    static void CleanupExtractDirectory(string extractRoot)
    {
        if (!Directory.Exists(extractRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(extractRoot, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AssetBundlePackExtractor] 清理解压目录失败: " + ex.Message);
        }
    }

    static void ExtractZipToDirectory(string zipPath, string extractRoot)
    {
        using (FileStream stream = File.OpenRead(zipPath))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                // entry.FullName 应为相对 BundleName；未校验是否逃出 extractRoot（Zip Slip）。
                string destinationPath = Path.Combine(extractRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                string destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                using (Stream entryStream = entry.Open())
                using (FileStream outputStream = File.Create(destinationPath))
                {
                    entryStream.CopyTo(outputStream);
                }
            }
        }
    }

    static bool VerifyBundleFile(string filePath, CustomAssetBundleInfo bundleInfo)
    {
        if (bundleInfo.Size > 0)
        {
            long actualSize = new FileInfo(filePath).Length;
            if (actualSize != bundleInfo.Size)
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(bundleInfo.Hash))
        {
            return MD5Checker.VerifyFileMD5(filePath, bundleInfo.Hash);
        }

        return true;
    }

    static IEnumerator ConvertToLz4Coroutine(string sourcePath, string targetPath, Action<bool, string> onComplete)
    {
        string targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var compression = BuildCompression.LZ4Runtime;
        var operation = AssetBundle.RecompressAssetBundleAsync(
            sourcePath,
            targetPath,
            compression,
            0,
            ThreadPriority.Normal);

        while (!operation.isDone)
        {
            yield return null;
        }

        if (File.Exists(sourcePath))
        {
            File.Delete(sourcePath);
        }

        if (!operation.success)
        {
            onComplete?.Invoke(false, "格式转换失败");
            yield break;
        }

        AssetBundle bundle = AssetBundle.LoadFromFile(targetPath);
        if (bundle == null)
        {
            onComplete?.Invoke(false, "转换后的 AB 包加载失败");
            yield break;
        }

        bundle.Unload(false);
        onComplete?.Invoke(true, string.Empty);
    }
}
