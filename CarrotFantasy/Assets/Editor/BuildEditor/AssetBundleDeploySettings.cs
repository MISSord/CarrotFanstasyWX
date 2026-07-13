using UnityEditor;
using UnityEngine;

/// <summary>
/// AB 云服务器 SFTP 上传配置。
/// 全部落在本机 EditorPrefs，不写进工程文件；默认值为空，避免真实主机信息进入 Git。
/// </summary>
public static class AssetBundleDeploySettings
{
    public const int DefaultPort = 22;

    const string PrefsHost = "AB_DeployHost";
    const string PrefsPort = "AB_DeployPort";
    const string PrefsUser = "AB_DeployUser";
    const string PrefsPassword = "AB_DeployPassword";
    const string PrefsRemotePath = "AB_DeployRemotePath";
    const string PrefsUsePrivateKey = "AB_DeployUsePrivateKey";
    const string PrefsPrivateKeyPath = "AB_DeployPrivateKeyPath";
    const string PrefsPromptAfterBuild = "AB_DeployPromptAfterBuild";

    public static string Host
    {
        get => EditorPrefs.GetString(PrefsHost, string.Empty);
        set => EditorPrefs.SetString(PrefsHost, value ?? string.Empty);
    }

    public static int Port
    {
        get => EditorPrefs.GetInt(PrefsPort, DefaultPort);
        set => EditorPrefs.SetInt(PrefsPort, Mathf.Clamp(value, 1, 65535));
    }

    public static string User
    {
        get => EditorPrefs.GetString(PrefsUser, string.Empty);
        set => EditorPrefs.SetString(PrefsUser, value ?? string.Empty);
    }

    public static string Password
    {
        get => EditorPrefs.GetString(PrefsPassword, string.Empty);
        set => EditorPrefs.SetString(PrefsPassword, value ?? string.Empty);
    }

    public static string RemotePath
    {
        get => EditorPrefs.GetString(PrefsRemotePath, string.Empty);
        set => EditorPrefs.SetString(PrefsRemotePath, NormalizeRemotePath(value));
    }

    public static bool UsePrivateKey
    {
        get => EditorPrefs.GetBool(PrefsUsePrivateKey, false);
        set => EditorPrefs.SetBool(PrefsUsePrivateKey, value);
    }

    public static string PrivateKeyPath
    {
        get => EditorPrefs.GetString(PrefsPrivateKeyPath, string.Empty);
        set => EditorPrefs.SetString(PrefsPrivateKeyPath, value ?? string.Empty);
    }

    public static bool PromptUploadAfterBuild
    {
        get => EditorPrefs.GetBool(PrefsPromptAfterBuild, true);
        set => EditorPrefs.SetBool(PrefsPromptAfterBuild, value);
    }

    public static string NormalizeRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }

    public static bool TryValidate(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            errorMessage = "服务器地址不能为空（保存在本机 EditorPrefs，不会提交到 Git）。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(User))
        {
            errorMessage = "用户名不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemotePath))
        {
            errorMessage = "远程目录不能为空。";
            return false;
        }

        if (UsePrivateKey)
        {
            if (string.IsNullOrWhiteSpace(PrivateKeyPath))
            {
                errorMessage = "已启用私钥登录，但未选择私钥文件。";
                return false;
            }

            if (!System.IO.File.Exists(PrivateKeyPath))
            {
                errorMessage = "私钥文件不存在: " + PrivateKeyPath;
                return false;
            }
        }
        else if (string.IsNullOrEmpty(Password))
        {
            errorMessage = "请填写 SSH 密码，或改用私钥登录。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
