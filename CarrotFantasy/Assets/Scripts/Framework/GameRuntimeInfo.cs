using UnityEngine;

/// <summary>
/// 运行时全局信息。放各处共用的加载模式判断，避免再各自读 EditorPrefs。
/// 菜单：Tools/编辑器/游戏加载模式设置。正式包没有该菜单，始终走 AB。
/// </summary>
public static class GameRuntimeInfo
{
    /// <summary>与 <see cref="LoadMode"/>、<c>GameLoadMode</c> 共用的 EditorPrefs 键。</summary>
    public const string LoadModePrefsKey = "GameLoadMode";

    /// <summary>
    /// 当前是否通过 AssetBundle 加载资源。
    /// 正式包恒为 true。编辑器里只有 Development / DebugMode 直读工程，返回 false。
    /// </summary>
    public static bool UsesAssetBundle
    {
        get { return !IsEditorDirectLoad; }
    }

    /// <summary>编辑器直读 AssetDatabase，不经真实 AB。正式包恒为 false。</summary>
    public static bool IsEditorDirectLoad
    {
        get
        {
#if UNITY_EDITOR
            LoadMode mode = CurrentLoadMode;
            return mode == LoadMode.Development || mode == LoadMode.DebugMode;
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>编辑器当前加载模式。正式包不编译这个属性。</summary>
    public static LoadMode CurrentLoadMode
    {
        get { return (LoadMode)UnityEditor.EditorPrefs.GetInt(LoadModePrefsKey, (int)LoadMode.Development); }
    }
#endif

    /// <summary>
    /// 这份资源是不是不能 Destroy 的编辑器资产。
    /// 不走 AB 时拿到的就是工程资产。
    /// 编辑器里即使走 AB，LoadAllAssets 仍可能交回工程原资产，那种也要留着。
    /// </summary>
    public static bool IsPersistentEditorAsset(Object asset)
    {
        if (asset == null)
        {
            return false;
        }

        if (!UsesAssetBundle)
        {
            return true;
        }

#if UNITY_EDITOR
        return UnityEditor.EditorUtility.IsPersistent(asset);
#else
        return false;
#endif
    }
}
