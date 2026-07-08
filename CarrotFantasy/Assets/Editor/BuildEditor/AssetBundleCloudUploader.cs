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

        List<LocalUploadFile> files = CollectFiles(localDirectory);
        if (files.Count == 0)
        {
            result.Message = "本地目录中没有可上传的文件。";
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

                using FileStream stream = File.OpenRead(file.FullPath);
                client.UploadFile(stream, remoteFilePath, true);

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

    static List<LocalUploadFile> CollectFiles(string localDirectory)
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

            var info = new FileInfo(fullPath);
            files.Add(new LocalUploadFile
            {
                FullPath = fullPath,
                RelativePath = relativePath,
                Size = info.Length,
            });
        }

        files.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return files;
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
