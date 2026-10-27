#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>AB 相关 Editor 快捷菜单。</summary>
public static class AssetBundlePathMenu
{
    const string OpenPcDownloadMenu = "Tools/AssetBundle/打开 PC AB 下载目录 _F10";

    [MenuItem(OpenPcDownloadMenu, false, 310)]
    public static void OpenPcAbDownloadDirectory()
    {
        AssetBundlePathHelper.Initialize();
        string downloadDir = AssetBundlePathHelper.GetPersistentDownloadDirectory();
        EnsureDirectory(downloadDir);
        EditorUtility.RevealInFinder(downloadDir);
        Debug.Log("[AB Tools] PC AB 下载目录: " + downloadDir);
    }

    [MenuItem("Tools/AssetBundle/打开 PC 持久化数据目录", false, 311)]
    public static void OpenPcPersistentDataDirectory()
    {
        string persistentDir = Application.persistentDataPath;
        EnsureDirectory(persistentDir);
        EditorUtility.RevealInFinder(persistentDir);
        Debug.Log("[AB Tools] PC 持久化目录: " + persistentDir);
    }

    static void EnsureDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
#endif
