using System;
using System.Collections.Generic;
using System.IO;
using Renci.SshNet;
using UnityEditor;
using UnityEngine;

public sealed class AssetBundleCloudUploadResult
{
    public bool Success;
    public string Message = string.Empty;
    public int UploadedFileCount;
    public long UploadedBytes;
    public bool Cancelled;
}

/// <summary>通过 SFTP 将本地 AB 目录上传到云服务器。</summary>
public static class AssetBundleCloudUploader
{
    public static AssetBundleCloudUploadResult UploadDirectory(string localDirectory)
    {
        return UploadDirectory(localDirectory, relativePathFilter: null);
    }

    /// <param name="relativePathFilter">返回 true 表示上传；null 表示全部上传。</param>
    public static AssetBundleCloudUploadResult UploadDirectory(
        string localDirectory,
        Func<string, bool> relativePathFilter)
    {
        var result = new AssetBundleCloudUploadResult();

        if (!AssetBundleDeploySettings.TryValidate(out string validationError))
        {
            result.Message = validationError;
            return result;
        }

        if (string.IsNullOrEmpty(localDirectory) || !Directory.Exists(localDirectory))
        {
            result.Message = "本地 AB 目录不存在: " + localDirectory;
            return result;
        }

        List<LocalUploadFile> files = CollectFiles(localDirectory, relativePathFilter);
        if (files.Count == 0)
        {
            result.Message = "本地目录中没有可上传的文件。";
            return result;
        }

        if (!HasManifestSignature(files, out string signatureError))
        {
            result.Message = signatureError;
            return result;
        }

        long totalBytes = 0;
        for (int i = 0; i < files.Count; i++)
        {
            totalBytes += files[i].Size;
        }

        string remoteRoot = AssetBundleDeploySettings.NormalizeRemotePath(AssetBundleDeploySettings.RemotePath);
        long uploadedBytes = 0;
        int uploadedCount = 0;

        try
        {
            using SftpClient client = CreateClient();
            client.Connect();
            EnsureRemoteDirectory(client, remoteRoot);

            for (int i = 0; i < files.Count; i++)
            {
                LocalUploadFile file = files[i];
                float progress = totalBytes > 0
                    ? (float)uploadedBytes / totalBytes
                    : (float)i / files.Count;

                string progressText = string.Format(
                    "{0} ({1}/{2})",
                    file.RelativePath,
                    i + 1,
                    files.Count);

                if (EditorUtility.DisplayCancelableProgressBar("上传 AB 到云服务器", progressText, progress))
                {
                    result.Cancelled = true;
                    result.Message = "用户已取消上传。";
                    result.UploadedFileCount = uploadedCount;
                    result.UploadedBytes = uploadedBytes;
                    client.Disconnect();
                    return result;
                }

                string remoteFilePath = CombineRemotePath(remoteRoot, file.RelativePath);
                string remoteDirectory = GetRemoteDirectory(remoteFilePath);
                EnsureRemoteDirectory(client, remoteDirectory);

                UploadFileAtomic(client, file.FullPath, remoteFilePath, IsManifestFile(file.RelativePath));

                uploadedCount++;
                uploadedBytes += file.Size;
            }

            client.Disconnect();
            result.Success = true;
            result.UploadedFileCount = uploadedCount;
            result.UploadedBytes = uploadedBytes;
            result.Message = string.Format(
                "已上传 {0} 个文件，共 {1}。",
                uploadedCount,
                FormatBytes(uploadedBytes));
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.UploadedFileCount = uploadedCount;
            result.UploadedBytes = uploadedBytes;
            result.Message = ex.Message;
            Debug.LogException(ex);
            return result;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>代码热更：只上传 hybridclr / 清单 / version / packs。</summary>
    public static bool IsCodeHotUpdateUploadPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return false;
        }

        string path = relativePath.Replace('\\', '/').ToLowerInvariant();
        if (path == "custom_manifest.json"
            || path == "custom_manifest.json.sig"
            || path == "version.txt")
        {
            return true;
        }

        if (path.StartsWith("hybridclr/", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.StartsWith("packs/", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    static SftpClient CreateClient()
    {
        string host = AssetBundleDeploySettings.Host.Trim();
        int port = AssetBundleDeploySettings.Port;
        string user = AssetBundleDeploySettings.User.Trim();

        AuthenticationMethod authMethod;
        if (AssetBundleDeploySettings.UsePrivateKey)
        {
            PrivateKeyFile keyFile = string.IsNullOrEmpty(AssetBundleDeploySettings.Password)
                ? new PrivateKeyFile(AssetBundleDeploySettings.PrivateKeyPath)
                : new PrivateKeyFile(AssetBundleDeploySettings.PrivateKeyPath, AssetBundleDeploySettings.Password);
            authMethod = new PrivateKeyAuthenticationMethod(user, keyFile);
        }
        else
        {
            authMethod = new PasswordAuthenticationMethod(user, AssetBundleDeploySettings.Password);
        }

        var connectionInfo = new ConnectionInfo(host, port, user, authMethod)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };

        return new SftpClient(connectionInfo);
    }

    static List<LocalUploadFile> CollectFiles(
        string localDirectory,
        Func<string, bool> relativePathFilter)
    {
        var files = new List<LocalUploadFile>();
        string root = Path.GetFullPath(localDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (string fullPath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            if (fullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relativePath = relativePath.Replace('\\', '/');

            if (relativePathFilter != null && !relativePathFilter(relativePath))
            {
                continue;
            }

            var info = new FileInfo(fullPath);
            files.Add(new LocalUploadFile
            {
                FullPath = fullPath,
                RelativePath = relativePath,
                Size = info.Length,
            });
        }

        files.Sort(CompareUploadOrder);
        return files;
    }

    /// <summary>内容先传。清单、签名和 version 放在最后，避免线上清单指向还没传完的文件。</summary>
    static int CompareUploadOrder(LocalUploadFile a, LocalUploadFile b)
    {
        int rankA = ManifestUploadRank(a.RelativePath);
        int rankB = ManifestUploadRank(b.RelativePath);
        if (rankA != rankB)
        {
            return rankA.CompareTo(rankB);
        }

        return string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    static int ManifestUploadRank(string relativePath)
    {
        string path = string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath.Replace('\\', '/');
        if (string.Equals(path, "version.txt", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(path, ManifestSignature.SignatureFileName, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (string.Equals(path, "custom_manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 0;
    }

    static bool IsManifestFile(string relativePath)
    {
        string path = string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath.Replace('\\', '/');
        return string.Equals(path, "custom_manifest.json", StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, ManifestSignature.SignatureFileName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, "version.txt", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>清单必须和 .sig 成对上传，否则客户端验签失败会拒绝更新。</summary>
    static bool HasManifestSignature(List<LocalUploadFile> files, out string error)
    {
        error = null;
        bool hasManifest = false;
        bool hasSignature = false;
        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i].RelativePath.Replace('\\', '/');
            if (string.Equals(path, "custom_manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                hasManifest = true;
            }
            else if (string.Equals(path, ManifestSignature.SignatureFileName, StringComparison.OrdinalIgnoreCase))
            {
                hasSignature = true;
            }
        }

        if (hasManifest && !hasSignature)
        {
            error = "清单缺少签名文件 " + ManifestSignature.SignatureFileName
                    + "：客户端验签会失败。请先执行 AB 打包（GenerateManifest 会自动签名）。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 先传成 .uploading 再改名，避免客户端读到写了一半的文件。
    /// 清单/签名切换失败时保留旧文件不动，避免出现"没有清单可用"的窗口。
    /// </summary>
    static void UploadFileAtomic(SftpClient client, string localPath, string remotePath, bool keepRemoteOnFailure)
    {
        string tempRemotePath = remotePath + ".uploading";
        using (FileStream stream = File.OpenRead(localPath))
        {
            client.UploadFile(stream, tempRemotePath, true);
        }

        try
        {
            // OpenSSH 默认支持 posix-rename，可覆盖式原子改名。
            client.RenameFile(tempRemotePath, remotePath, true);
            return;
        }
        catch (Exception renameError)
        {
            if (keepRemoteOnFailure)
            {
                throw new IOException(
                    "内容已上传但清单切换失败，旧清单保持不动。请确认服务器支持 posix-rename，"
                    + "或手动把 " + tempRemotePath + " 改名为 " + remotePath
                    + "。原始错误: " + renameError.Message,
                    renameError);
            }

            Debug.LogWarning("[AB Upload] posix-rename 失败，回退为删除+改名: " + remotePath + " (" + renameError.Message + ")");
        }

        if (client.Exists(remotePath))
        {
            client.DeleteFile(remotePath);
        }

        client.RenameFile(tempRemotePath, remotePath);
    }

    static void EnsureRemoteDirectory(SftpClient client, string remoteDirectory)
    {
        if (string.IsNullOrEmpty(remoteDirectory) || remoteDirectory == "/")
        {
            return;
        }

        string normalized = remoteDirectory.Replace('\\', '/');
        if (client.Exists(normalized))
        {
            return;
        }

        int lastSlash = normalized.LastIndexOf('/');
        if (lastSlash > 0)
        {
            EnsureRemoteDirectory(client, normalized.Substring(0, lastSlash));
        }

        client.CreateDirectory(normalized);
    }

    static string CombineRemotePath(string remoteRoot, string relativePath)
    {
        return remoteRoot + "/" + relativePath.Replace('\\', '/');
    }

    static string GetRemoteDirectory(string remoteFilePath)
    {
        int lastSlash = remoteFilePath.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return "/";
        }

        return remoteFilePath.Substring(0, lastSlash);
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024f).ToString("F1") + " KB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return (bytes / 1024f / 1024f).ToString("F2") + " MB";
        }

        return (bytes / 1024f / 1024f / 1024f).ToString("F2") + " GB";
    }

    sealed class LocalUploadFile
    {
        public string FullPath = string.Empty;
        public string RelativePath = string.Empty;
        public long Size;
    }
}
