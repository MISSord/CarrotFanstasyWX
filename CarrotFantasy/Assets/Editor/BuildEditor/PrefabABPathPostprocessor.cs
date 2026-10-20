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

    private static bool ShouldAssignAssetBundle(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        bool isPrefab = IsViewLikePrefab(path);
        bool isRawImage = path.Contains("UI/RawImage");
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

        // 动画控制器：按目录成包（animator/animatorcontroller/monster/{关卡}），
        // 资源名 = 文件名（怪物 id），供 BattleUnitView_Monster 运行时按 id 取。
        // 动画片段 .anim 仍不单独成包，会作为依赖并入控制器所在的包。
        if (path.EndsWith(".controller", System.StringComparison.OrdinalIgnoreCase))
            return normalizedBundleName;

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
}
