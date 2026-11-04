using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入或移动 Assets 下资源时写入 assetBundleName。
/// 目录本身绝不写包名，否则子资源会被两个包认领，还会留下清不掉的幽灵包名。
/// 包名没变就不写，避免导入一致性检查跑两遍时产物哈希对不上。
/// </summary>
public class PrefabABPathPostprocessor : AssetPostprocessor
{
    const string GameRoot = "Assets/Game";
    const string LuaScriptBundleName = "lua_scripts";
    const string SharedAnimatorBundle = "animator/shared_prefab";
    const string ImagesAtlasSuffix = "/images_atlas";

    static readonly string[] GameFolderExcludedExtensions =
    {
        ".meta", ".cs", ".lua", ".unity", ".anim",
        ".asmdef", ".asmref", ".dll", ".pdb", ".mdb",
    };

    void OnPreprocessAsset()
    {
        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return;

        TryApplyBundleName(assetPath);
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        for (int i = 0; i < movedAssets.Length; i++)
            TryApplyBundleName(movedAssets[i]);
    }

    /// <summary>
    /// 写入包名。返回是否改了 importer。
    /// 解析结果为空也会写回去，用来清掉不再适用的旧包名。
    /// </summary>
    static bool TryApplyBundleName(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return false;

        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null)
            return false;

        string bundleName = ResolveBundleName(path);
        if (string.Equals(importer.assetBundleName, bundleName, StringComparison.Ordinal))
            return false;

        importer.assetBundleName = bundleName;
        return true;
    }

    /// <summary>
    /// 按优先级解析包名。空字符串表示这个资源不进 AB。
    /// 1. 固定名（Lua / Luban / 战斗合包 / 共享动画）
    /// 2. 目录合包（预制体、控制器、片段 → *_prefab）
    /// 3. 图集与散图
    /// 4. Assets/Game 兜底 *_prefab
    /// </summary>
    static string ResolveBundleName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        string normalized = path.Replace('\\', '/');
        string lower = normalized.ToLowerInvariant();
        string extension = Path.GetExtension(lower);

        if (lower.EndsWith("/lua_bundle.bytes", StringComparison.Ordinal))
            return LuaScriptBundleName;

        if (extension == ".json" && ContainsIgnoreCase(normalized, "/Config/Luban/"))
            return CarrotFantasy.LubanConfigAbPaths.BundleName;

        string grouped = TryGetGroupedFightPartBundle(normalized, extension);
        if (grouped != null)
            return grouped;

        if (IsSharedAnimatorAsset(normalized, extension))
            return SharedAnimatorBundle;

        string folder = GetGameRelativeFolder(normalized);
        if (string.IsNullOrEmpty(folder))
            return string.Empty;

        string folderLower = folder.ToLowerInvariant();

        if (IsViewLikePrefab(normalized, extension) || IsFightPrefabBundleMate(normalized, extension))
            return folderLower + "_prefab";

        if (extension == ".spriteatlas")
            return StripTrailingImagesFolder(folderLower) + "/" + Path.GetFileNameWithoutExtension(lower);

        if (IsAtlasSourceImage(lower, extension))
            return StripTrailingImagesFolder(folderLower) + ImagesAtlasSuffix;

        if (IsPngJpg(extension) && lower.Contains("/ui/rawimages/"))
            return "ui/rawimages/" + Path.GetFileNameWithoutExtension(lower);

        if (IsPngJpg(extension) && lower.Contains("/ui/sprites/"))
            return BuildUiSpritesBundleName(normalized);

        // 父目录 .../monster 与子目录 .../monster/1 不能一个写文件一个写目录，统一加 _prefab。
        if (UseGameFolderRule(normalized, lower, extension))
            return folderLower + "_prefab";

        return string.Empty;
    }

    /// <summary>
    /// 战斗资源按实际加载范围合包：同一塔/同一关/全部特效各一个包。
    /// 子文件夹可以，同级的 Tower_1_1 和 Tower_1_2 不行。
    /// </summary>
    static string TryGetGroupedFightPartBundle(string normalized, string extension)
    {
        const string PrefabMarker = "/FightPart/FightPrefab/";
        int prefabIndex = normalized.IndexOf(PrefabMarker, StringComparison.OrdinalIgnoreCase);
        if (prefabIndex >= 0)
        {
            string tail = normalized.Substring(prefabIndex + PrefabMarker.Length);
            int towerId = ReadGroupId(tail, "Tower/");
            if (towerId > 0)
                return "fightpart/fightprefab/tower/" + towerId + "_prefab";

            int bulletId = ReadGroupId(tail, "Bullet/");
            if (bulletId > 0)
                return "fightpart/fightprefab/bullet/" + bulletId + "_prefab";

            int monsterLevel = ReadGroupId(tail, "Monster/Level_");
            if (monsterLevel > 0)
                return "fightpart/fightprefab/monster/level_" + monsterLevel + "_prefab";

            if (tail.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
                return "fightpart/fightprefab/effect_prefab";

            return null;
        }

        const string AtlasMarker = "/FightPart/AltasSprite/";
        int atlasIndex = normalized.IndexOf(AtlasMarker, StringComparison.OrdinalIgnoreCase);
        if (atlasIndex < 0)
            return null;

        if (!IsAtlasPackable(extension))
            return null;

        string atlasTail = normalized.Substring(atlasIndex + AtlasMarker.Length);
        int towerAtlasId = ReadGroupId(atlasTail, "Tower/");
        if (towerAtlasId > 0)
            return "fightpart/altassprite/tower/" + towerAtlasId + ImagesAtlasSuffix;

        int monsterLevelFolder = ReadGroupId(atlasTail, "Monster/Level_");
        if (monsterLevelFolder > 0)
            return "fightpart/altassprite/monster/level_" + monsterLevelFolder + ImagesAtlasSuffix;

        if (atlasTail.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            return "fightpart/altassprite/effect" + ImagesAtlasSuffix;

        if (atlasTail.StartsWith("Carrot/", StringComparison.OrdinalIgnoreCase))
            return "fightpart/altassprite/carrot" + ImagesAtlasSuffix;

        return null;
    }

    static int ReadGroupId(string tail, string prefix)
    {
        if (string.IsNullOrEmpty(tail) || !tail.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return -1;

        int start = prefix.Length;
        int end = start;
        while (end < tail.Length && char.IsDigit(tail[end]))
            end++;

        if (end == start)
            return -1;

        if (end < tail.Length)
        {
            char next = tail[end];
            if (next != '_' && next != '/' && next != '.')
                return -1;
        }

        return int.Parse(tail.Substring(start, end - start));
    }

    /// <summary>资源所在目录相对 Assets/Game。不在 Game 下时返回完整目录，供兜底规则使用。</summary>
    static string GetGameRelativeFolder(string normalizedPath)
    {
        string directory = Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrEmpty(directory))
            return null;

        string relativePath = directory.Replace('\\', '/');
        if (string.Equals(relativePath, GameRoot, StringComparison.OrdinalIgnoreCase))
            return "root_assets";

        if (relativePath.StartsWith(GameRoot + "/", StringComparison.OrdinalIgnoreCase))
            return relativePath.Substring(GameRoot.Length + 1);

        return relativePath;
    }

    static string StripTrailingImagesFolder(string folderLower)
    {
        const string ImagesFolder = "/images";
        if (folderLower.EndsWith(ImagesFolder, StringComparison.Ordinal))
            return folderLower.Substring(0, folderLower.Length - ImagesFolder.Length);

        return folderLower;
    }

    /// <summary>Assets/Game/UI/Sprites/{rel}/name.png → ui/sprites/{rel}/name</summary>
    static string BuildUiSpritesBundleName(string normalized)
    {
        const string Marker = "/UI/Sprites/";
        int marker = normalized.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        string rel = marker >= 0
            ? normalized.Substring(marker + Marker.Length)
            : normalized;
        rel = rel.ToLowerInvariant().Replace('\\', '/');

        string imageName = Path.GetFileNameWithoutExtension(rel);
        string dir = Path.GetDirectoryName(rel);
        if (string.IsNullOrEmpty(dir))
            return "ui/sprites/" + imageName;

        return "ui/sprites/" + dir.Replace('\\', '/') + "/" + imageName;
    }

    static bool IsViewLikePrefab(string normalized, string extension)
    {
        if (extension != ".prefab")
            return false;

        return normalized.IndexOf("UI/View", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("UI/FightPart", StringComparison.Ordinal) >= 0
            || normalized.IndexOf("Models", StringComparison.Ordinal) >= 0;
    }

    static bool IsFightPrefabBundleMate(string normalized, string extension)
    {
        if (normalized.IndexOf("/FightPart/FightPrefab/", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return extension == ".anim"
            || extension == ".controller"
            || extension == ".overridecontroller"
            || extension == ".spriteatlas";
    }

    static bool IsSharedAnimatorAsset(string normalized, string extension)
    {
        if (normalized.IndexOf("/Game/Animator/", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return extension == ".anim"
            || extension == ".controller"
            || extension == ".overridecontroller";
    }

    /// <summary>图集源图：当前层 png 打进该层 images_atlas，不各自成包。UI 散图目录除外。</summary>
    static bool IsAtlasSourceImage(string lower, string extension)
    {
        if (!IsPngJpg(extension))
            return false;

        if (lower.Contains("/ui/sprites/") || lower.Contains("/ui/rawimages/"))
            return false;

        return lower.Contains("/images/")
            || lower.IndexOf("/fightpart/altassprite/", StringComparison.Ordinal) >= 0;
    }

    static bool UseGameFolderRule(string normalized, string lower, string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;

        if (!lower.StartsWith("assets/game/", StringComparison.Ordinal))
            return false;

        if (extension == ".json" && ContainsIgnoreCase(normalized, "/Config/Luban/"))
            return false;

        if (IsAtlasSourceImage(lower, extension)
            || (IsPngJpg(extension) && (lower.Contains("/ui/sprites/") || lower.Contains("/ui/rawimages/"))))
            return false;

        for (int i = 0; i < GameFolderExcludedExtensions.Length; i++)
        {
            if (extension == GameFolderExcludedExtensions[i])
                return false;
        }

        return true;
    }

    static bool IsAtlasPackable(string extension)
    {
        return IsPngJpg(extension)
            || extension == ".tga"
            || extension == ".psd"
            || extension == ".spriteatlas";
    }

    static bool IsPngJpg(string extension)
    {
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
    }

    static bool ContainsIgnoreCase(string value, string fragment)
    {
        return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 按当前规则重算 Assets/Game 下所有资源的包名。
    /// 规则改过之后 .meta 里可能还是旧名，这个菜单一次改完，并清掉目录上误写的包名。
    /// </summary>
    [MenuItem("Tools/AssetBundle/重新分配所有资源包名", false, 302)]
    public static void ReassignAllBundleNames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Object", new[] { GameRoot });
        int scanned = 0;
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;

                scanned++;
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                string bundleName = ResolveBundleName(path);
                if (string.Equals(importer.assetBundleName, bundleName, StringComparison.Ordinal))
                    continue;

                importer.assetBundleName = bundleName;
                EditorUtility.SetDirty(importer);
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        int clearedFolders = 0;
        string[] folders = Directory.GetDirectories(GameRoot, "*", SearchOption.AllDirectories);
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < folders.Length; i++)
            {
                string folder = folders[i].Replace('\\', '/');
                AssetImporter folderImporter = AssetImporter.GetAtPath(folder);
                if (folderImporter == null || string.IsNullOrEmpty(folderImporter.assetBundleName))
                    continue;

                folderImporter.assetBundleName = string.Empty;
                EditorUtility.SetDirty(folderImporter);
                clearedFolders++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Debug.Log("[AB] 重新分配包名：扫描 " + scanned + " 个资源，更新 " + changed + " 个，清理目录包名 " + clearedFolders + " 个");
    }
}
