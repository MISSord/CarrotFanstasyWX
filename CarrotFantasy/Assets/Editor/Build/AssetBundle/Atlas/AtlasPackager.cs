using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>图集打包逻辑，供 AtlasPackerWindow 窗口与 AB 构建流水线共用。</summary>
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

    /// <summary>单个文件夹的图集打包结果，供 Project 窗口右键菜单展示。</summary>
    public struct FolderAtlasResult
    {
        /// <summary>右键选中的文件夹。</summary>
        public string RequestedFolder;

        /// <summary>图集实际所在文件夹；当前层没有图片时会回退到 Images 子文件夹。</summary>
        public string AtlasFolder;

        /// <summary>图集资源路径。</summary>
        public string AtlasPath;

        /// <summary>参与打包的图片数量。</summary>
        public int ImageCount;

        /// <summary>是否完成创建/更新。</summary>
        public bool Packed;
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

    /// <summary>
    /// 为右键选中的文件夹生成一个图集，产物固定为文件夹下的 <c>images_atlas.spriteatlas</c>。
    /// <para>
    /// 只取文件夹当前层的图片，不递归子文件夹：项目按「一层一图集」组织
    /// （如 UI/Images/Tower/1、Tower/2 各自持有 images_atlas），递归会把子文件夹的图
    /// 重复打进父图集。当前层没有图片、但存在 <c>Images</c> 子文件夹时（UI/View/* 的惯例），
    /// 回退到该子文件夹，图集仍然落在选中文件夹的下面。
    /// </para>
    /// </summary>
    public static FolderAtlasResult PackAtlasForFolder(string folderPath, bool includeSubdirectories = false)
    {
        var result = new FolderAtlasResult
        {
            RequestedFolder = NormalizeAssetPath(folderPath),
            AtlasFolder = string.Empty,
            AtlasPath = string.Empty,
            ImageCount = 0,
            Packed = false,
        };

        if (!AssetDatabase.IsValidFolder(result.RequestedFolder))
        {
            Debug.LogWarning("[AtlasPackager] 选中的不是有效文件夹: " + result.RequestedFolder);
            return result;
        }

        SearchOption searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string sourceFolder = result.RequestedFolder;
        string[] imagePaths = GetImagePathsInFolder(sourceFolder, searchOption);

        if (imagePaths.Length == 0)
        {
            string imagesFolder = sourceFolder + "/Images";
            if (AssetDatabase.IsValidFolder(imagesFolder))
            {
                string[] nestedImagePaths = GetImagePathsInFolder(imagesFolder, searchOption);
                if (nestedImagePaths.Length > 0)
                {
                    sourceFolder = imagesFolder;
                    imagePaths = nestedImagePaths;
                    Debug.Log("[AtlasPackager] 当前层没有图片，回退到 Images 子文件夹: " + imagesFolder);
                }
            }
        }

        result.AtlasFolder = sourceFolder;
        result.AtlasPath = sourceFolder + "/" + DefaultAtlasName + ".spriteatlas";
        result.ImageCount = imagePaths.Length;

        if (imagePaths.Length == 0)
        {
            Debug.LogWarning("[AtlasPackager] 文件夹及其 Images 子文件夹中都没有图片: " + result.RequestedFolder);
            return result;
        }

        result.Packed = PackImages(sourceFolder, DefaultAtlasName, imagePaths);
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

        return PackImages(relativeFolderPath, atlasName, GetImagePathsInFolder(relativeFolderPath, searchOption));
    }

    /// <summary>用已经解析好的图片列表创建/更新文件夹下的图集。</summary>
    private static bool PackImages(string relativeFolderPath, string atlasName, string[] imagePaths)
    {
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

    /// <summary>列出文件夹中参与图集的图片（绝对路径统一转成 Assets/... 形式）。</summary>
    private static string[] GetImagePathsInFolder(string folderPath, SearchOption searchOption)
    {
        return Directory.GetFiles(ToAbsolutePath(folderPath), "*.*", searchOption)
            .Select(ToAssetPath)
            .Where(IsImageFile)
            .ToArray();
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
        return GetImagePathsInFolder(folderPath, searchOption).Length > 0;
    }
}
