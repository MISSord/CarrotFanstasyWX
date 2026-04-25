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

        importer.assetBundleName = BuildAssetBundleName(assetPath);
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
            if (ShouldAssignAssetBundle(movedAsset))
            {
                AssetImporter importer = AssetImporter.GetAtPath(movedAsset);
                if (importer != null)
                {
                    importer.assetBundleName = BuildAssetBundleName(movedAsset);
                }
            }
        }
    }

    private static bool ShouldAssignAssetBundle(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        bool isPrefab = IsViewLikePrefab(path);
        bool isRawImage = path.Contains("UI/RawImage");
        bool isSpriteAtlas = path.EndsWith(".spriteatlas");
        bool isImageInImagesFolder = IsImageInImagesFolder(path);
        bool isImageInRawImagesFolder = IsImageInRawImagesFolder(path);
        bool useGameFolderRule = UseGameFolderRule(path);
        return isPrefab || isRawImage || isSpriteAtlas || isImageInImagesFolder || isImageInRawImagesFolder || useGameFolderRule;
    }

    private static string BuildAssetBundleName(string path)
    {
        if (!ShouldAssignAssetBundle(path))
            return string.Empty;

        string bundleName = GenerateBundleNameFromFolder(path);
        if (string.IsNullOrEmpty(bundleName))
            return string.Empty;

        string normalizedBundleName = bundleName.ToLower().Replace('\\', '/');

        if (IsViewLikePrefab(path))
            return normalizedBundleName + "_prefab";

        if (path.EndsWith(".spriteatlas"))
        {
            string atlasBundleRoot = normalizedBundleName;
            if (atlasBundleRoot.EndsWith("/images"))
            {
                atlasBundleRoot = atlasBundleRoot.Substring(0, atlasBundleRoot.Length - "/images".Length);
            }
            return atlasBundleRoot + "/" + Path.GetFileNameWithoutExtension(path);
        }

        if (IsImageInImagesFolder(path))
        {
            string atlasBundleRoot = normalizedBundleName;
            if (atlasBundleRoot.EndsWith("/images"))
            {
                atlasBundleRoot = atlasBundleRoot.Substring(0, atlasBundleRoot.Length - "/images".Length);
            }
            return atlasBundleRoot + "/images_atlas";
        }

        if (IsImageInRawImagesFolder(path))
        {
            string imageName = Path.GetFileNameWithoutExtension(path).ToLower();
            return "ui/rawimages/" + imageName;
        }

        if (UseGameFolderRule(path))
            return normalizedBundleName + "_prefab";

        string name = normalizedBundleName + "/" + Path.GetFileNameWithoutExtension(path);
        return name + "_prefab";
    }

    private static bool IsImageInImagesFolder(string path)
    {
        string normalizedPath = path.ToLower().Replace('\\', '/');
        bool isImage = normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg");
        return isImage && normalizedPath.Contains("/images/");
    }

    private static bool IsImageInRawImagesFolder(string path)
    {
        string normalizedPath = path.ToLower().Replace('\\', '/');
        bool isImage = normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg");
        return isImage && normalizedPath.Contains("/rawimages/");
    }

    private static bool IsViewLikePrefab(string path)
    {
        if (!path.EndsWith(".prefab"))
            return false;

        return path.Contains("UI/View") || path.Contains("UI/FightPart") || path.Contains("Models");
    }

    private static bool UseGameFolderRule(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalizedPath = path.ToLower().Replace('\\', '/');
        if (!normalizedPath.StartsWith("assets/game/"))
            return false;

        string extension = Path.GetExtension(normalizedPath);
        if (string.IsNullOrEmpty(extension))
            return false;

        // Skip source/metadata files that should not participate in AB naming.
        return extension != ".meta" &&
               extension != ".cs" &&
               extension != ".anim" &&
               extension != ".controller" &&
               extension != ".asmdef" &&
               extension != ".asmref" &&
               extension != ".dll" &&
               extension != ".pdb" &&
               extension != ".mdb";
    }
}
