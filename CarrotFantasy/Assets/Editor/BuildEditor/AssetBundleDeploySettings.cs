using UnityEditor;
using UnityEngine;

/// <summary>AB 云服务器 SFTP 上传配置（EditorPrefs）。</summary>
public static class AssetBundleDeploySettings
{
    public const string DefaultHost = "124.222.203.161";
    public const int DefaultPort = 22;
    public const string DefaultUser = "root";
    public const string DefaultRemotePath = "/var/www/carrotfantasy/ab/StandaloneWindows";

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
        get => EditorPrefs.GetString(PrefsHost, DefaultHost);
        set => EditorPrefs.SetString(PrefsHost, value ?? string.Empty);
    }

    public static int Port
    {
        get => EditorPrefs.GetInt(PrefsPort, DefaultPort);
        set => EditorPrefs.SetInt(PrefsPort, Mathf.Clamp(value, 1, 65535));
    }

    public static string User
    {
        get => EditorPrefs.GetString(PrefsUser, DefaultUser);
        set => EditorPrefs.SetString(PrefsUser, value ?? string.Empty);
    }

    public static string Password
    {
        get => EditorPrefs.GetString(PrefsPassword, string.Empty);
        set => EditorPrefs.SetString(PrefsPassword, value ?? string.Empty);
    }

    public static string RemotePath
    {
        get => EditorPrefs.GetString(PrefsRemotePath, DefaultRemotePath);
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
            return DefaultRemotePath;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }

    public static bool TryValidate(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            errorMessage = "服务器地址不能为空。";
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
