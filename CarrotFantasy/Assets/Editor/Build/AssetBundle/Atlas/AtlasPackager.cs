using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>图集打包逻辑，菜单 Tools/AssetBundle/图集打包 与 AB 构建流水线共用。</summary>
public static class AtlasPackager
{
    public const string UiViewRoot = "Assets/Game/UI/View";
    public const string UiImagesRoot = "Assets/Game/UI/Images";
    public const string AltasSpriteRoot = "Assets/Game/FightPart/AltasSprite";
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
        public PackResult AltasSpriteResult;
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

    /// <summary>
    /// 默认打包：
    /// UI/View 只处理名为 Images 的文件夹；UI/Images 与 FightPart/AltasSprite 每个目录一层一份图集。
    /// 都只收当前层的图，不进子文件夹。
    /// </summary>
    public static DefaultPackResult PackDefaultUiAtlases()
    {
        var result = new DefaultPackResult
        {
            ViewResult = PackAtlasForViewImagesFolders(UiViewRoot, includeSubdirectories: true),
            ImagesResult = PackAtlasForCurrentLayerFolders(UiImagesRoot, includeRoot: true),
            AltasSpriteResult = PackAtlasForCurrentLayerFolders(AltasSpriteRoot, includeRoot: true),
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return result;
    }

    [MenuItem("Tools/AssetBundle/图集打包", false, 302)]
    public static void PackFromMenu()
    {
        PackForAbBuild();
    }

    /// <summary>AB 构建前：View 只打 Images 文件夹；UI/Images 与 AltasSprite 每个目录一层一份图集。</summary>
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
        Debug.Log(string.Format(
            "[AB Build] AltasSprite 图集：检查 {0} 个，创建/更新 {1} 个，跳过 {2} 个。",
            result.AltasSpriteResult.ProcessedCount,
            result.AltasSpriteResult.CreatedCount,
            result.AltasSpriteResult.SkippedCount));
        Debug.Log("[AB Build] UI 图集打包完成。");
    }

    /// <summary>
    /// 自定义目标：<c>UI/Images</c> 与 <c>FightPart/AltasSprite</c> 每个目录一层一份图集；
    /// 其它目录只打包名为 Images 的文件夹。图集只收当前层的图片。
    /// </summary>
    public static PackResult PackAtlasForTargetFolder(string targetFolderPath, bool includeSubdirectories = true)
    {
        string normalizedTarget = NormalizeAssetPath(targetFolderPath);
        if (!AssetDatabase.IsValidFolder(normalizedTarget))
        {
            Debug.LogWarning("[AtlasPackager] 目标文件夹不存在: " + normalizedTarget);
            return new PackResult();
        }

        if (IsLayerAtlasRoot(normalizedTarget) || IsLayerAtlasChildFolder(normalizedTarget))
        {
            return PackAtlasForCurrentLayerFolders(normalizedTarget, includeRoot: true);
        }

        return PackAtlasForViewImagesFolders(normalizedTarget, includeSubdirectories);
    }

    /// <summary>
    /// UI/View：只处理名为 Images 的文件夹，当前层有图才打，不收子文件夹。
    /// </summary>
    public static PackResult PackAtlasForViewImagesFolders(string viewRoot, bool includeSubdirectories = true)
    {
        var result = new PackResult();
        string normalizedRoot = NormalizeAssetPath(viewRoot);
        if (!AssetDatabase.IsValidFolder(normalizedRoot))
        {
            Debug.LogWarning("[AtlasPackager] 目标文件夹不存在: " + normalizedRoot);
            return result;
        }

        List<string> folders = CollectAssetFolders(normalizedRoot, includeRoot: false, includeSubdirectories);
        for (int i = 0; i < folders.Count; i++)
        {
            if (!IsImagesFolderName(folders[i]))
            {
                continue;
            }

            TryPackCurrentLayerFolder(folders[i], ref result);
        }

        return result;
    }

    /// <summary>UI/Images 根目录入口，行为与 <see cref="PackAtlasForCurrentLayerFolders"/> 相同。</summary>
    public static PackResult PackAtlasForUiImagesSubfolders(string uiImagesRoot)
    {
        return PackAtlasForCurrentLayerFolders(uiImagesRoot, includeRoot: true);
    }

    /// <summary>
    /// 每个文件夹一层一份图集：当前层有图才打，没有就不打，并删掉多余的旧图集。
    /// 不把子文件夹里的图收进父图集。
    /// </summary>
    public static PackResult PackAtlasForCurrentLayerFolders(string targetFolderPath, bool includeRoot)
    {
        var result = new PackResult();
        string normalizedTarget = NormalizeAssetPath(targetFolderPath);
        if (!AssetDatabase.IsValidFolder(normalizedTarget))
        {
            Debug.LogWarning("[AtlasPackager] 目标文件夹不存在: " + normalizedTarget);
            return result;
        }

        List<string> folders = CollectAssetFolders(normalizedTarget, includeRoot, includeSubdirectories: true);
        for (int i = 0; i < folders.Count; i++)
        {
            TryPackCurrentLayerFolder(folders[i], ref result);
        }

        return result;
    }

    /// <summary>为目标文件夹下每个目录生成当前层图集（含子目录，但不把子目录的图打进父图集）。</summary>
    public static PackResult PackAtlasForEachSubfolder(string targetFolderPath)
    {
        return PackAtlasForCurrentLayerFolders(targetFolderPath, includeRoot: false);
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

        string sourceFolder = result.RequestedFolder;
        string[] imagePaths = GetImagePathsInFolder(sourceFolder, SearchOption.TopDirectoryOnly);

        if (imagePaths.Length == 0)
        {
            string imagesFolder = sourceFolder + "/Images";
            if (AssetDatabase.IsValidFolder(imagesFolder))
            {
                string[] nestedImagePaths = GetImagePathsInFolder(imagesFolder, SearchOption.TopDirectoryOnly);
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
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
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
            UnityEngine.Object[] existingPackables = atlas.GetPackables();
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

    /// <summary>当前层有图才打包；没有图则跳过，并删掉该层多余的 images_atlas。</summary>
    static void TryPackCurrentLayerFolder(string folderPath, ref PackResult result)
    {
        result.ProcessedCount++;
        if (!HasImagesInFolder(folderPath, SearchOption.TopDirectoryOnly))
        {
            if (TryDeleteOrphanAtlas(folderPath))
            {
                Debug.Log("[AtlasPackager] 当前层没有图片，已删除多余图集: " + GetAtlasAssetPath(folderPath));
            }

            result.SkippedCount++;
            return;
        }

        if (PackImagesInFolder(folderPath, DefaultAtlasName, SearchOption.TopDirectoryOnly))
        {
            result.CreatedCount++;
        }
        else
        {
            result.SkippedCount++;
        }
    }

    static List<string> CollectAssetFolders(string rootAssetPath, bool includeRoot, bool includeSubdirectories)
    {
        var folders = new List<string>();
        if (includeRoot)
        {
            folders.Add(rootAssetPath);
        }

        string absoluteRoot = ToAbsolutePath(rootAssetPath);
        if (!Directory.Exists(absoluteRoot))
        {
            return folders;
        }

        SearchOption searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] directories = Directory.GetDirectories(absoluteRoot, "*", searchOption);
        for (int i = 0; i < directories.Length; i++)
        {
            folders.Add(ToAssetPath(directories[i]));
        }

        return folders;
    }

    static bool IsImagesFolderName(string folderPath)
    {
        string name = Path.GetFileName(NormalizeAssetPath(folderPath));
        return string.Equals(name, "Images", StringComparison.OrdinalIgnoreCase);
    }

    static string GetAtlasAssetPath(string folderPath)
    {
        return NormalizeAssetPath(folderPath) + "/" + DefaultAtlasName + ".spriteatlas";
    }

    static bool TryDeleteOrphanAtlas(string folderPath)
    {
        string atlasPath = GetAtlasAssetPath(folderPath);
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(atlasPath)))
        {
            return false;
        }

        return AssetDatabase.DeleteAsset(atlasPath);
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

    static bool IsLayerAtlasRoot(string folderPath)
    {
        string normalizedPath = folderPath.ToLowerInvariant().Replace("\\", "/");
        return normalizedPath.EndsWith("/ui/images")
            || normalizedPath.EndsWith("/fightpart/altassprite");
    }

    static bool IsLayerAtlasChildFolder(string folderPath)
    {
        string normalizedPath = folderPath.ToLowerInvariant().Replace("\\", "/");
        bool underUiImages = normalizedPath.Contains("/ui/images/") && !normalizedPath.EndsWith("/ui/images");
        bool underAltasSprite = normalizedPath.Contains("/fightpart/altassprite/")
            && !normalizedPath.EndsWith("/fightpart/altassprite");
        return underUiImages || underAltasSprite;
    }

    private static bool HasImagesInFolder(string folderPath, SearchOption searchOption)
    {
        return GetImagePathsInFolder(folderPath, searchOption).Length > 0;
    }
}
