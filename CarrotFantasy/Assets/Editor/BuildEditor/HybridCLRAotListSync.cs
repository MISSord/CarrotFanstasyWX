#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 <c>HybridCLRSettings.patchAOTAssemblies</c> 与 HybridCLR 生成的
/// <c>AOTGenericReferences.PatchedAOTAssemblyList</c> 对齐。
/// <para>
/// 为什么不直接引用 <c>AOTGenericReferences</c>：它在 <c>Assembly-CSharp</c> 里，
/// 而本目录属于 <c>Unity.Editor</c> 程序集定义；asmdef 无法反向引用预定义程序集，
/// 直接写类名会编译失败。所以这里改为解析它的源文件文本。
/// </para>
/// </summary>
public static class HybridCLRAotListSync
{
    private const string MenuPath = "Tools/HybridCLR/AOT 名单对齐 AOTGenericReferences";

    private const string ReferencesRelativePath = "HybridCLRGenerate/AOTGenericReferences.cs";

    [MenuItem(MenuPath, false, 121)]
    private static void Sync()
    {
        HybridCLRSettings settings = HybridCLRSettings.Instance;
        if (settings == null)
        {
            Debug.LogError("[HybridCLRAotListSync] 取不到 HybridCLRSettings，请确认 HybridCLR 包已正常加载。");
            return;
        }

        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) 保留用户已经配置的条目
        if (settings.patchAOTAssemblies != null)
        {
            for (int i = 0; i < settings.patchAOTAssemblies.Length; i++)
            {
                string name = Normalize(settings.patchAOTAssemblies[i]);
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                {
                    merged.Add(name);
                }
            }
        }

        int existingCount = merged.Count;

        // 2) 并入 AOTGenericReferences 里列出的程序集
        List<string> referenced = ReadReferencedAssemblies(out string readError);
        if (readError != null)
        {
            Debug.LogError("[HybridCLRAotListSync] " + readError);
            return;
        }

        for (int i = 0; i < referenced.Count; i++)
        {
            string name = Normalize(referenced[i]);
            if (!string.IsNullOrEmpty(name) && seen.Add(name))
            {
                merged.Add(name);
            }
        }

        merged.Sort(StringComparer.OrdinalIgnoreCase);
        settings.patchAOTAssemblies = merged.ToArray();
        HybridCLRSettings.Save();

        Debug.Log(string.Format(
            "[HybridCLRAotListSync] patchAOTAssemblies 已更新：原有 {0} 项，现共 {1} 项。\n  {2}",
            existingCount,
            merged.Count,
            string.Join(", ", merged.ToArray())));

        EditorUtility.DisplayDialog(
            "AOT 名单已对齐",
            string.Format("patchAOTAssemblies 现在有 {0} 项：\n{1}\n\n下一步请执行 HybridCLR/Generate/All。",
                merged.Count,
                string.Join("\n", merged.ToArray())),
            "确定");
    }

    /// <summary>从 AOTGenericReferences.cs 的注释/字符串里抽取所有 "xxx.dll"。</summary>
    private static List<string> ReadReferencedAssemblies(out string error)
    {
        error = null;
        var result = new List<string>();

        string fullPath = Path.Combine(Application.dataPath, ReferencesRelativePath);
        if (!File.Exists(fullPath))
        {
            error = "找不到 " + ReferencesRelativePath + "，请先执行一次 HybridCLR/Generate/All。";
            return result;
        }

        try
        {
            string text = File.ReadAllText(fullPath);
            foreach (Match match in Regex.Matches(text, "\"([A-Za-z0-9_.]+\\.[Dd][Ll][Ll])\""))
            {
                string name = match.Groups[1].Value;
                if (!result.Contains(name))
                {
                    result.Add(name);
                }
            }
        }
        catch (Exception e)
        {
            error = "读取 " + ReferencesRelativePath + " 失败: " + e.Message;
        }

        if (result.Count == 0 && error == null)
        {
            error = ReferencesRelativePath + " 里没有解析到任何 AOT 程序集，请检查该文件是否被清空。";
        }

        return result;
    }

    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string value = name.Trim();
        if (value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 4);
        }

        return value;
    }
}
#endif
