using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>图集打包逻辑，供 AtlasPacker 窗口与 AB 构建流水线共用。</summary>
public static class AtlasPackager
{
    public const string UiViewRoot = "Assets/Game/UI/View";
    public const string UiImagesRoot = "Assets/Game/UI/Images";
    public const string DefaultAtlasName = "images_atlas";

    public struct PackResult
    {
        public int ProcessedCount;
        public int CreatedCount;
        public int SkippedCount;
    }

    public struct DefaultPackResult
    {
        public PackResult ViewResult;
        public PackResult ImagesResult;
    }

    /// <summary>默认打包：UI/View（TargetFolder）+ UI/Images（EachSubfolder）。</summary>
    public static DefaultPackResult PackDefaultUiAtlases()
    {
        var result = new DefaultPackResult
        {
            ViewResult = PackAtlasForTargetFolder(UiViewRoot, includeSubdirectories: true),
            ImagesResult = PackAtlasForEachSubfolder(UiImagesRoot),
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return result;
    }

    /// <summary>AB 构建前：View 走 TargetFolder 逻辑，Images 走 EachSubfolder 逻辑。</summary>
    public static void PackForAbBuild()
    {
        Debug.Log("[AB Build] 开始打包 UI 图集…");

        DefaultPackResult result = PackDefaultUiAtlases();

        Debug.Log(string.Format(
            "[AB Build] UI/View 图集：检查 {0} 个，创建/更新 {1} 个，跳过 {2} 个。",
            result.ViewResult.ProcessedCount,
            result.ViewResult.CreatedCount,
            result.ViewResult.SkippedCount));
        Debug.Log(string.Format(
            "[AB Build] UI/Images 图集：检查 {0} 个，创建/更新 {1} 个，跳过 {2} 个。",
            result.ImagesResult.ProcessedCount,
            result.ImagesResult.CreatedCount,
            result.ImagesResult.SkippedCount));
        Debug.Log("[AB Build] UI 图集打包完成。");
    }

    /// <summary>遍历目标文件夹，为包含 Images 子文件夹的路径打包图集（含 UI/Images 特殊规则）。</summary>
    public static PackResult PackAtlasForTargetFolder(string targetFolderPath, bool includeSubdirectories = true)
    {
        var result = new PackResult();
        string normalizedTarget = NormalizeAssetPath(targetFolderPath);
        if (!AssetDatabase.IsValidFolder(normalizedTarget))
        {
            Debug.LogWarning("[AtlasPackager] 目标文件夹不存在: " + normalizedTarget);
            return result;
        }

        if (IsUiImagesRoot(normalizedTarget))
        {
            return PackAtlasForUiImagesSubfolders(normalizedTarget);
        }

        string absoluteTarget = ToAbsolutePath(normalizedTarget);
        string[] allSubfolders = Directory.GetDirectories(
            absoluteTarget,
            "*",
            includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        foreach (string folderPath in allSubfolders)
        {
            string assetFolderPath = ToAssetPath(folderPath);
            if (IsUiImagesChildFolder(assetFolderPath) && HasImagesInFolder(assetFolderPath, SearchOption.TopDirectoryOnly))
            {
                result.ProcessedCount++;
                if (PackImagesInFolder(assetFolderPath, DefaultAtlasName, SearchOption.TopDirectoryOnly))
                {
                    result.CreatedCount++;
                }
                else
                {
                    result.SkippedCount++;
                }

                continue;
            }

            string imageFolderPath = Path.Combine(assetFolderPath, "Images").Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(imageFolderPath))
            {
                continue;
            }

            if (IsUiImagesRoot(imageFolderPath))
            {
                continue;
            }

            result.ProcessedCount++;
            if (PackImagesInFolder(imageFolderPath, DefaultAtlasName))
            {
                result.CreatedCount++;
            }
            else
            {
                result.SkippedCount++;
            }
        }

        return result;
    }

    public static PackResult PackAtlasForUiImagesSubfolders(string uiImagesRoot)
    {
        var result = new PackResult();
        string normalizedRoot = NormalizeAssetPath(uiImagesRoot);
        if (!AssetDatabase.IsValidFolder(normalizedRoot))
        {
            Debug.LogWarning("[AtlasPackager] UI/Images 根目录不存在: " + normalizedRoot);
            return result;
        }

        string absoluteRoot = ToAbsolutePath(normalizedRoot);
        string[] imageSubfolders = Directory.GetDirectories(absoluteRoot, "*", SearchOption.TopDirectoryOnly);
        foreach (string folderPath in imageSubfolders)
        {
            string assetFolderPath = ToAssetPath(folderPath);
            result.ProcessedCount++;
            if (PackImagesInFolder(assetFolderPath, DefaultAtlasName, SearchOption.TopDirectoryOnly))
            {
                result.CreatedCount++;
            }
            else
            {
                result.SkippedCount++;
            }
        }

        return result;
    }

    /// <summary>为目标文件夹下每个第一层子文件夹生成独立图集。</summary>
    public static PackResult PackAtlasForEachSubfolder(string targetFolderPath)
    {
        var result = new PackResult();
        string normalizedTarget = NormalizeAssetPath(targetFolderPath);
        if (!AssetDatabase.IsValidFolder(normalizedTarget))
        {
            Debug.LogWarning("[AtlasPackager] 目标文件夹不存在: " + normalizedTarget);
            return result;
        }

        string absoluteTarget = ToAbsolutePath(normalizedTarget);
        string[] firstLevelSubfolders = Directory.GetDirectories(absoluteTarget, "*", SearchOption.TopDirectoryOnly);
        foreach (string folderPath in firstLevelSubfolders)
        {
            string assetFolderPath = ToAssetPath(folderPath);
            result.ProcessedCount++;
            if (PackImagesInFolder(assetFolderPath, DefaultAtlasName))
            {
                result.CreatedCount++;
            }
            else
            {
                result.SkippedCount++;
            }
        }

        return result;
    }

    public static bool PackImagesInFolder(
        string folderPath,
        string atlasName,
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        string relativeFolderPath = NormalizeAssetPath(folderPath);
        if (!AssetDatabase.IsValidFolder(relativeFolderPath))
        {
            return false;
        }

        string[] imagePaths = Directory.GetFiles(ToAbsolutePath(relativeFolderPath), "*.*", searchOption)
            .Select(ToAssetPath)
            .Where(IsImageFile)
            .ToArray();

        if (imagePaths.Length == 0)
        {
            Debug.LogWarning("[AtlasPackager] 文件夹中没有图片: " + relativeFolderPath);
            return false;
        }

        Debug.Log(string.Format("[AtlasPackager] {0} 中找到 {1} 张图片", relativeFolderPath, imagePaths.Length));

        string atlasPath = Path.Combine(relativeFolderPath, atlasName + ".spriteatlas").Replace("\\", "/");
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }
        else
        {
            Object[] existingPackables = atlas.GetPackables();
            if (existingPackables != null && existingPackables.Length > 0)
            {
                atlas.Remove(existingPackables);
            }
        }

        ApplyDefaultAtlasSettings(atlas);

        List<Texture2D> textures = new List<Texture2D>();
        foreach (string imagePath in imagePaths)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        if (textures.Count == 0)
        {
            Debug.LogWarning("[AtlasPackager] 未能加载任何纹理: " + relativeFolderPath);
            return false;
        }

        atlas.Add(textures.ToArray());
        EditorUtility.SetDirty(atlas);
        Debug.Log("[AtlasPackager] 创建/更新图集: " + atlasPath);
        return true;
    }

    private static void ApplyDefaultAtlasSettings(SpriteAtlas atlas)
    {
        SpriteAtlasPackingSettings packingSettings = new SpriteAtlasPackingSettings
        {
            padding = 4,
            enableRotation = false,
            enableTightPacking = true,
        };
        atlas.SetPackingSettings(packingSettings);

        SpriteAtlasTextureSettings textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false,
            generateMipMaps = false,
            filterMode = FilterMode.Bilinear,
        };
        atlas.SetTextureSettings(textureSettings);
    }

    private static string NormalizeAssetPath(string path)
    {
        string normalized = (path ?? string.Empty).Replace("\\", "/");
        if (normalized.StartsWith("Assets/"))
        {
            return normalized;
        }

        if (normalized.StartsWith(Application.dataPath.Replace("\\", "/")))
        {
            return "Assets" + normalized.Substring(Application.dataPath.Length);
        }

        return normalized;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string normalized = NormalizeAssetPath(assetPath);
        if (normalized.StartsWith("Assets/"))
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalized));
        }

        return Path.GetFullPath(normalized);
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalized = absolutePath.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (normalized.StartsWith(dataPath))
        {
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        return normalized;
    }

    private static bool IsImageFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
    }

    private static bool IsUiImagesRoot(string folderPath)
    {
        string normalizedPath = folderPath.ToLowerInvariant().Replace("\\", "/");
        return normalizedPath.EndsWith("/ui/images");
    }

    private static bool IsUiImagesChildFolder(string folderPath)
    {
        string normalizedPath = folderPath.ToLowerInvariant().Replace("\\", "/");
        return normalizedPath.Contains("/ui/images/") && !normalizedPath.EndsWith("/ui/images");
    }

    private static bool HasImagesInFolder(string folderPath, SearchOption searchOption)
    {
        return Directory.GetFiles(ToAbsolutePath(folderPath), "*.*", searchOption).Any(IsImageFile);
    }
}
