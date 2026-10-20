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

        // 目录也会触发本回调。一旦给目录写了包名，会在 GetAllAssetBundleNames() 里
        // 留下永远清不掉的幽灵包名（如 ui/rawimages_prefab），并可能让子资源被两个包认领。
        if (AssetDatabase.IsValidFolder(assetPath))
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

        // 注意要用 GameRoot + "/" 做前缀：只写 StartsWith("Assets/Game/") 会漏掉
        // 直接放在 Assets/Game 下的资源（如 URP 管线资产），拼出 assets/game_prefab 这种怪名字。
        const string GameRoot = "Assets/Game";
        string relativePath = directory.Replace('\\', '/');
        if (string.Equals(relativePath, GameRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
        }
        else if (relativePath.StartsWith(GameRoot + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath.Substring(GameRoot.Length + 1);
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

    private static bool IsImageFilePath(string path)
    {
        string lower = path.ToLowerInvariant();
        return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")
            || lower.EndsWith(".tga") || lower.EndsWith(".psd");
    }

    private static bool ShouldAssignAssetBundle(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        bool isPrefab = IsViewLikePrefab(path);
        // 只对图片文件生效：这是旧的宽松路径判断（UI/RawImage 单数目录）。
        // 若不限定扩展名，目录（如 UI/RawImages）也会被判成资源并写入包名，
        // 而「目录带包名」会让它下面所有资源隐式并入该包，造成重复打包。
        bool isRawImage = path.Contains("UI/RawImage") && IsImageFilePath(path);
        bool isSpriteAtlas = path.EndsWith(".spriteatlas");
        bool isImageInImagesFolder = IsImageInImagesFolder(path);
        bool isImageInRawImagesFolder = IsImageInRawImagesFolder(path);
        bool isImageInSpritesFolder = IsImageInSpritesFolder(path);
        bool useGameFolderRule = UseGameFolderRule(path);
        return isPrefab || isRawImage || isSpriteAtlas || isImageInImagesFolder || isImageInRawImagesFolder || isImageInSpritesFolder || IsLubanConfigJson(path) || useGameFolderRule;
    }

    private static string BuildAssetBundleName(string path)
    {
        if (!ShouldAssignAssetBundle(path))
            return string.Empty;

        // Lua 脚本单独成包：改得最频繁、体积又小，热更时只下这几百 KB，不必动整个 AB。
        if (IsLuaScriptAsset(path))
            return LuaScriptBundleName;

        if (IsLubanConfigJson(path))
            return CarrotFantasy.LubanConfigAbPaths.BundleName;

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

        if (IsImageInSpritesFolder(path))
        {
            // Assets/Game/UI/Sprites/{relPath}/name.png -> ui/sprites/{relPath}/name
            string normalizedPath = path.Replace('\\', '/');
            int marker = normalizedPath.IndexOf("/UI/Sprites/", System.StringComparison.OrdinalIgnoreCase);
            string rel = marker >= 0
                ? normalizedPath.Substring(marker + "/UI/Sprites/".Length)
                : normalizedPath;
            rel = rel.ToLowerInvariant();
            string imageName = Path.GetFileNameWithoutExtension(rel);
            string dir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;
            return string.IsNullOrEmpty(dir) ? "ui/sprites/" + imageName : "ui/sprites/" + dir + "/" + imageName;
        }

        // 动画控制器也走下面的 _prefab 约定：若直接返回 normalizedBundleName，
        // 父目录会得到 .../monster，子目录得到 .../monster/1，
        // Unity 无法同时在 .../monster 写文件又在 .../monster/ 写目录 → 打包失败。
        // 加 _prefab 后缀后：.../monster_prefab（文件）与 .../monster/1_prefab（文件）不再冲突。
        if (UseGameFolderRule(path))
            return normalizedBundleName + "_prefab";

        string name = normalizedBundleName + "/" + Path.GetFileNameWithoutExtension(path);
        return name + "_prefab";
    }

    /// <summary>Lua Bundle 产物归属的 AB 包名；与 LuaBundleReader.BundleName 对应。</summary>
    private const string LuaScriptBundleName = "lua_scripts";

    /// <summary>
    /// 只有打包产物 lua_bundle.bytes 进 AB。
    /// 源码 *.lua 不被 Unity 导入（无 .meta、无资产），因此不会命中这里。
    /// </summary>
    private static bool IsLuaScriptAsset(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.EndsWith("/lua_bundle.bytes", System.StringComparison.Ordinal);
    }

    private static bool IsImageInImagesFolder(string path)
    {
        string normalizedPath = path.ToLower().Replace('\\', '/');
        bool isImage = normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg");
        // Atlas source images only; never treat ui/sprites or ui/rawimages as atlas inputs.
        return isImage
               && normalizedPath.Contains("/images/")
               && !normalizedPath.Contains("/ui/sprites/")
               && !normalizedPath.Contains("/ui/rawimages/");
    }

    private static bool IsImageInRawImagesFolder(string path)
    {
        string normalizedPath = path.ToLower().Replace('\\', '/');
        bool isImage = normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg");
        return isImage && normalizedPath.Contains("/ui/rawimages/");
    }

    private static bool IsImageInSpritesFolder(string path)
    {
        string normalizedPath = path.ToLower().Replace('\\', '/');
        bool isImage = normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg");
        return isImage && normalizedPath.Contains("/ui/sprites/");
    }

    private static bool IsViewLikePrefab(string path)
    {
        if (!path.EndsWith(".prefab"))
            return false;

        return path.Contains("UI/View") || path.Contains("UI/FightPart") || path.Contains("Models");
    }

    private static bool IsLubanConfigJson(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            return false;

        string normalizedPath = path.Replace('\\', '/');
        return normalizedPath.IndexOf("/Config/Luban/", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool UseGameFolderRule(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (IsLubanConfigJson(path))
            return false;

        string normalizedPath = path.ToLower().Replace('\\', '/');
        if (!normalizedPath.StartsWith("assets/game/"))
            return false;

        string extension = Path.GetExtension(normalizedPath);
        if (string.IsNullOrEmpty(extension))
            return false;

        // Image folders have dedicated naming rules; never fall through to *_prefab.
        if (IsImageInSpritesFolder(path) || IsImageInRawImagesFolder(path) || IsImageInImagesFolder(path))
            return false;

        // Skip source/metadata files that should not participate in AB naming.
        // .unity 必须排除：场景要留在 Build Settings 里由 SceneManager.LoadScene 加载，
        // 一旦被绑进 AB，既会与 Build Settings 冲突，AB 里的场景也永远加载不到。
        return extension != ".meta" &&
               extension != ".cs" &&
               extension != ".unity" &&
               extension != ".anim" &&
               extension != ".asmdef" &&
               extension != ".asmref" &&
               extension != ".dll" &&
               extension != ".pdb" &&
               extension != ".mdb";
    }
    /// <summary>
    /// 按当前规则重新分配 Assets/Game 下所有资源的 AB 包名。
    /// <para>
    /// 规则改动后（例如把 .controller 纳入打包），已导入资源的 .meta 里仍是旧包名；
    /// 逐个目录手动 Reimport 容易漏，这个菜单一次性重算。
    /// </para>
    /// </summary>
    [MenuItem("Tools/AssetBundle/重新分配所有资源包名", false, 302)]
    public static void ReassignAllBundleNames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Object", new[] { "Assets/Game" });
        int scanned = 0;
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) || !ShouldAssignAssetBundle(path))
                {
                    continue;
                }

                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    continue;
                }

                scanned++;
                string bundleName = BuildAssetBundleName(path);
                if (string.Equals(importer.assetBundleName, bundleName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                importer.assetBundleName = bundleName;
                EditorUtility.SetDirty(importer);
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 目录本身绝不能有包名：会把它下面所有资源隐式并入该包，造成重复打包。
        // 这里顺手清掉历史误写（旧规则把 UI/RawImages 这类目录也判成了 rawimage）。
        int clearedFolders = 0;
        string[] folders = Directory.GetDirectories("Assets/Game", "*", SearchOption.AllDirectories);
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < folders.Length; i++)
            {
                string folder = folders[i].Replace('\\', '/');
                AssetImporter folderImporter = AssetImporter.GetAtPath(folder);
                if (folderImporter == null || string.IsNullOrEmpty(folderImporter.assetBundleName))
                {
                    continue;
                }

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
        Debug.Log($"[AB] 重新分配包名：扫描 {scanned} 个资源，更新 {changed} 个，清理目录包名 {clearedFolders} 个");
    }
}
