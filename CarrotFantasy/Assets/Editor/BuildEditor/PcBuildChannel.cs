using System;
using System.Collections.Generic;
using System.Linq;
using CarrotFantasy;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// PC 构建通道：CF_DEV_TOOLS 宏 + ab_runtime_config.env。
/// 可只「准备」后手打 Build Settings，或走 PcPlayerBuildPipeline 一键出包。
/// </summary>
public static class PcBuildChannel
{
    const string MenuRoot = "Tools/Build Channel/";
    static readonly NamedBuildTarget StandaloneTarget = NamedBuildTarget.Standalone;

    [MenuItem(MenuRoot + "准备开发 PC 包（启用 CF_DEV_TOOLS + env=dev）", priority = 10)]
    public static void PrepareDevPcBuild()
    {
        ApplyChannel(
            enableDevTools: true,
            env: BuildChannelDefines.EnvDev,
            developmentBuildHint: true);
    }

    [MenuItem(MenuRoot + "准备正式 PC 包（禁用 CF_DEV_TOOLS + env=prod）", priority = 11)]
    public static void PrepareReleasePcBuild()
    {
        ApplyChannel(
            enableDevTools: false,
            env: BuildChannelDefines.EnvProd,
            developmentBuildHint: false);
    }

    /// <summary>
    /// 写入通道（宏 + runtime config）。返回 true 表示宏发生了变更（需等待脚本重编）。
    /// </summary>
    public static bool ApplyChannelCore(bool enableDevTools, string env)
    {
        bool defineChanged = SetDevToolsDefine(enableDevTools);
        AssetBundleBuildSettings.WriteRuntimeConfig(
            EditorUserBuildSettings.activeBuildTarget,
            env);
        return defineChanged;
    }

    [MenuItem(MenuRoot + "仅启用 CF_DEV_TOOLS", priority = 50)]
    public static void EnableDevToolsOnly()
    {
        SetDevToolsDefine(true);
        LogCurrentState("已启用 CF_DEV_TOOLS");
        EditorUtility.DisplayDialog(
            "CF_DEV_TOOLS",
            "已启用。请重新编译后同步热更 DLL（Tools/HybridCLR/同步 DLL）。\n\n"
            + "当前宏:\n" + GetDefinesDisplay(),
            "确定");
    }

    [MenuItem(MenuRoot + "仅禁用 CF_DEV_TOOLS", priority = 51)]
    public static void DisableDevToolsOnly()
    {
        SetDevToolsDefine(false);
        LogCurrentState("已禁用 CF_DEV_TOOLS");
        EditorUtility.DisplayDialog(
            "CF_DEV_TOOLS",
            "已禁用。正式包请用无宏状态重编 AOT + 热更 DLL。\n\n"
            + "当前宏:\n" + GetDefinesDisplay(),
            "确定");
    }

    [MenuItem(MenuRoot + "写入运行配置 env=dev", priority = 70)]
    public static void WriteEnvDev()
    {
        AssetBundleBuildSettings.WriteRuntimeConfig(
            EditorUserBuildSettings.activeBuildTarget,
            BuildChannelDefines.EnvDev);
    }

    [MenuItem(MenuRoot + "写入运行配置 env=prod", priority = 71)]
    public static void WriteEnvProd()
    {
        AssetBundleBuildSettings.WriteRuntimeConfig(
            EditorUserBuildSettings.activeBuildTarget,
            BuildChannelDefines.EnvProd);
    }

    [MenuItem(MenuRoot + "查看当前通道状态", priority = 100)]
    public static void ShowStatus()
    {
        bool hasDevTools = HasDevToolsDefine();
        string message =
            "CF_DEV_TOOLS: " + (hasDevTools ? "已启用（开发工具会进包）" : "未启用（正式裁剪）") + "\n\n"
            + "Standalone 宏:\n" + GetDefinesDisplay() + "\n\n"
            + "Development Build 勾选建议: " + (hasDevTools ? "开发包可勾选" : "正式包请勿勾选") + "\n\n"
            + "注意: 切换宏后需等待脚本编译，再执行 HybridCLR Generate/同步 DLL，"
            + "保证 AOT 与热更 DLL 宏一致。";

        EditorUtility.DisplayDialog("Build Channel 状态", message, "确定");
        LogCurrentState("状态查询");
    }

    static void ApplyChannel(bool enableDevTools, string env, bool developmentBuildHint)
    {
        bool defineChanged = ApplyChannelCore(enableDevTools, env);

        string title = enableDevTools ? "开发 PC 包已准备" : "正式 PC 包已准备";
        string body =
            "1) CF_DEV_TOOLS = " + (enableDevTools ? "ON" : "OFF") + "\n"
            + "2) ab_runtime_config.env = " + env + "\n"
            + "3) Development Build 建议: " + (developmentBuildHint ? "勾选" : "不要勾选") + "\n\n"
            + (defineChanged ? "宏已变更，请先等待脚本编译完成。\n\n" : string.Empty)
            + "接下来可选:\n"
            + "- Tools/Build Channel/一键打" + (enableDevTools ? "开发" : "正式") + " PC 包（推荐）\n"
            + "- 或手动: HybridCLR Generate/同步 DLL → File/Build Settings\n\n"
            + "当前宏:\n" + GetDefinesDisplay();

        LogCurrentState(title);
        bool open = EditorUtility.DisplayDialog(title, body, "打开 Build Settings", "稍后");
        if (open)
        {
            EditorApplication.ExecuteMenuItem("File/Build Settings...");
        }
    }

    public static bool HasDevToolsDefine()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(StandaloneTarget);
        return ContainsDefine(defines, BuildChannelDefines.DevTools);
    }

    /// <returns>true 表示宏发生了变更。</returns>
    public static bool SetDevToolsDefine(bool enabled)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(StandaloneTarget) ?? string.Empty;
        var list = SplitDefines(defines);
        bool changed = false;

        if (enabled)
        {
            if (!list.Exists(d => string.Equals(d, BuildChannelDefines.DevTools, StringComparison.Ordinal)))
            {
                list.Add(BuildChannelDefines.DevTools);
                changed = true;
            }
        }
        else
        {
            int removed = list.RemoveAll(d =>
                string.Equals(d, BuildChannelDefines.DevTools, StringComparison.Ordinal));
            changed = removed > 0;
        }

        if (!changed)
        {
            Debug.Log("[PcBuildChannel] CF_DEV_TOOLS 已是目标状态: " + enabled);
            return false;
        }

        string joined = string.Join(";", list);
        PlayerSettings.SetScriptingDefineSymbols(StandaloneTarget, joined);
        Debug.Log("[PcBuildChannel] 已更新 Standalone 宏: " + joined);
        return true;
    }

    static List<string> SplitDefines(string defines)
    {
        if (string.IsNullOrWhiteSpace(defines))
        {
            return new List<string>();
        }

        return defines
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    static bool ContainsDefine(string defines, string symbol)
    {
        return SplitDefines(defines).Exists(d => string.Equals(d, symbol, StringComparison.Ordinal));
    }

    static string GetDefinesDisplay()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(StandaloneTarget);
        return string.IsNullOrWhiteSpace(defines) ? "(无)" : defines.Replace(";", "\n");
    }

    static void LogCurrentState(string tag)
    {
        Debug.Log(
            "[PcBuildChannel] " + tag
            + " | CF_DEV_TOOLS=" + HasDevToolsDefine()
            + " | defines=" + PlayerSettings.GetScriptingDefineSymbols(StandaloneTarget));
    }
}
