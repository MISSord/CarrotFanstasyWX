#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 开发期一键开关清单签名校验（编译期宏 <c>CF_DEV_SKIP_MANIFEST_SIGN</c>）。
/// <para>开启后：打包不再要求私钥、上传不要求 .sig、客户端不下载也不校验 .sig。</para>
/// <para>
/// 该宏只在 Editor / Development / CF_DEV_TOOLS 构建下有效（见
/// <see cref="ManifestSignature.IsVerificationBypassed"/>），正式包即使残留也会强制验签。
/// </para>
/// </summary>
public static class ManifestSignDevToggle
{
    public const string DefineSymbol = "CF_DEV_SKIP_MANIFEST_SIGN";

    private const string MenuPath = "Tools/AssetBundle/开发期跳过清单签名";

    [MenuItem(MenuPath, false, 320)]
    private static void Toggle()
    {
        SetEnabled(!IsEnabled());
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsEnabled());
        return true;
    }

    private static bool IsEnabled()
    {
        List<BuildTargetGroup> groups = CollectGroups();
        for (int i = 0; i < groups.Count; i++)
        {
            if (HasSymbol(groups[i], DefineSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetEnabled(bool enabled)
    {
        List<BuildTargetGroup> groups = CollectGroups();
        for (int i = 0; i < groups.Count; i++)
        {
            ApplySymbol(groups[i], DefineSymbol, enabled);
        }

        AssetDatabase.Refresh();

        if (enabled)
        {
            Debug.LogWarning("[ManifestSignDevToggle] 已开启开发期跳过清单签名（宏 " + DefineSymbol
                             + "），Unity 正在重新编译。发布前务必再次点击关闭！");
        }
        else
        {
            Debug.Log("[ManifestSignDevToggle] 已关闭开发期跳过清单签名（宏 " + DefineSymbol
                      + "），Unity 正在重新编译。之后打包需要 Keys/manifest_signing_private.xml。");
        }
    }

    /// <summary>同时作用于当前激活平台组与 Standalone（工程只出 PC/macOS 包）。</summary>
    private static List<BuildTargetGroup> CollectGroups()
    {
        var groups = new List<BuildTargetGroup>();
        AddGroup(groups, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
        AddGroup(groups, BuildTargetGroup.Standalone);
        return groups;
    }

    private static void AddGroup(List<BuildTargetGroup> groups, BuildTargetGroup group)
    {
        if (group == BuildTargetGroup.Unknown || groups.Contains(group))
        {
            return;
        }

        groups.Add(group);
    }

    private static bool HasSymbol(BuildTargetGroup group, string symbol)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
        string[] parts = defines.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Trim() == symbol)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplySymbol(BuildTargetGroup group, string symbol, bool enabled)
    {
        NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(group);
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);

        var kept = new List<string>();
        string[] parts = defines.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            string item = parts[i].Trim();
            if (item.Length > 0 && item != symbol)
            {
                kept.Add(item);
            }
        }

        if (enabled)
        {
            kept.Add(symbol);
        }

        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", kept.ToArray()));
    }
}
#endif
