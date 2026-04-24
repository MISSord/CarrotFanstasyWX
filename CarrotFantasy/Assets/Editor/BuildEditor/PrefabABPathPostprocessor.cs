using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabABPathPostprocessor : AssetPostprocessor
{
    /// <summary>Assign assetBundleName for prefab imports under certain UI/Model paths.</summary>
    void OnPreprocessAsset()
    {
        if (!assetPath.StartsWith("Assets/"))
            return;

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return;

        if ((assetPath.Contains("UI/View") || assetPath.Contains("Models")) && assetPath.EndsWith(".prefab"))
        {
            string bundleName = GenerateBundleNameFromFolder(assetPath);
            if (string.IsNullOrEmpty(bundleName))
                return;

            importer.assetBundleName = bundleName.ToLower().Replace('\\', '/') + "_prefab";
        }
        else if(assetPath.Contains("UI/RawImage"))
        {
            string bundleName = GenerateBundleNameFromFolder(assetPath);
            if (string.IsNullOrEmpty(bundleName))
                return;
            string name = bundleName.ToLower().Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(assetPath);
            importer.assetBundleName = name + "_prefab";
        }
        else
        {
            importer.assetBundleName = string.Empty;
        }
    }

    private static string GenerateBundleNameFromFolder(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);

        if (string.IsNullOrEmpty(directory))
            return null;

        string relativePath = directory.Replace('\\', '/');
        if (relativePath.StartsWith("Assets/Game/"))
        {
            relativePath = relativePath.Substring(12);
        }

        if (string.IsNullOrEmpty(relativePath))
        {
            relativePath = "root_assets";
        }

        return relativePath;
    }

    /// <summary>Refresh bundle name when a prefab is moved on disk.</summary>
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string movedAsset in movedAssets)
        {
            if (movedAsset.EndsWith(".prefab"))
            {
                AssetImporter importer = AssetImporter.GetAtPath(movedAsset);
                if (importer != null)
                {
                    string newBundleName = GenerateBundleNameFromFolder(movedAsset);
                    if (!string.IsNullOrEmpty(newBundleName))
                    {
                        importer.assetBundleName = newBundleName.ToLower().Replace('\\', '/');
                    }
                }
            }
        }
    }
}
