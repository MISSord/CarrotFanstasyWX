using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using XLua;

namespace CarrotFantasy.Editor
{
    /// <summary>
    /// 把 <c>Assets/Game/LuaScripts/**/*.lua</c> 打包成单个 Lua Bundle 资产。
    /// <para>
    /// 打包时会做三件事（**全部在内存中完成，绝不改动源文件**）：
    ///   1) <b>扁平化</b>：模块名只保留文件名（framework/class → class），
    ///      解包后所有脚本落在同一个目录，不需要还原子目录；
    ///   2) <b>重写 require</b>：<c>require('framework.class')</c> → <c>require('class')</c>，
    ///      保持调用形式（引号、括号）不变，只替换字面量内容；
    ///   3) <b>语法检查</b>：对**重写后**的源码编译一次，有任何错误立即中止打包。
    /// </para>
    /// <para>
    /// 扁平化带来一个约束：<b>所有 .lua 的文件名必须全局唯一</b>。
    /// 出现重名会直接中止打包并列出冲突文件，而不是静默覆盖。
    /// </para>
    /// </summary>
    public static class LuaBundleBuilder
    {
        /// <summary>源码根目录（相对工程根）。</summary>
        public const string SourceRoot = "Assets/Game/LuaScripts";

        /// <summary>产物路径。放在源码目录下，便于打包规则按路径识别。</summary>
        public const string BundleAssetPath = SourceRoot + "/lua_bundle.bytes";

        /// <summary>排查用中间副本的落盘位置（Library 不进版本库）。</summary>
        public const string DebugDumpFolder = "Library/LuaBundleTemp";

        private static readonly byte[] Magic = { (byte)'C', (byte)'F', (byte)'L', (byte)'B' };

        /// <summary>匹配 require 后的字符串字面量；保留引号与括号原样。</summary>
        private static readonly Regex RequireLiteral = new Regex(
            @"(?<head>require\s*\(?\s*)(?<quote>['""])(?<module>[A-Za-z0-9_./\-]+)\k<quote>",
            RegexOptions.Compiled);

        /// <summary>匹配动态 require（参数是变量），这类无法在打包期静态替换。</summary>
        private static readonly Regex RequireDynamic = new Regex(
            @"require\s*\(?\s*[A-Za-z_][A-Za-z0-9_]*\s*[\),]",
            RegexOptions.Compiled);

        private struct LuaEntry
        {
            public string ModulePath;   // framework/class
            public string FlatName;     // class
            public string SourcePath;   // 绝对路径
            public string Source;       // 重写后的源码
        }

        [MenuItem("Tools/Lua/打包 Lua Bundle（扁平化 + 语法检查）", priority = 10)]
        private static void BuildFromMenu()
        {
            if (Build(out string summary))
            {
                EditorUtility.DisplayDialog("Lua Bundle", summary, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("Lua Bundle", "打包失败：\n\n" + summary, "确定");
            }
        }

        [MenuItem("Tools/Lua/打包 Lua Bundle（并导出中间副本，排查用）", priority = 11)]
        private static void BuildAndDumpFromMenu()
        {
            if (Build(out string summary, dumpTemp: true))
            {
                EditorUtility.DisplayDialog(
                    "Lua Bundle",
                    summary + "\n\n中间副本已导出到:\n" + DebugDumpFolder,
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("Lua Bundle", "打包失败：\n\n" + summary, "确定");
            }
        }

        /// <summary>执行打包。返回是否成功；summary 为给人看的结论。</summary>
        public static bool Build(out string summary, bool dumpTemp = false)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                summary = "无法定位工程根目录";
                Debug.LogError("[LuaBundle] " + summary);
                return false;
            }

            string absSourceRoot = Path.Combine(projectRoot, SourceRoot);
            if (!Directory.Exists(absSourceRoot))
            {
                summary = "源码目录不存在: " + SourceRoot;
                Debug.LogError("[LuaBundle] " + summary);
                return false;
            }

            string[] files = Directory.GetFiles(absSourceRoot, "*.lua", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            if (files.Length == 0)
            {
                summary = SourceRoot + " 下没有找到任何 .lua 文件";
                Debug.LogWarning("[LuaBundle] " + summary);
                return false;
            }

            // ---------- 1) 收集 + 扁平名唯一性校验 ----------
            List<LuaEntry> entries = new List<LuaEntry>(files.Length);
            Dictionary<string, List<string>> flatToPaths =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string modulePath = ModulePathOf(absSourceRoot, files[i]);
                string flatName = LuaBundleReader.ToFlatName(modulePath);

                List<string> bucket;
                if (!flatToPaths.TryGetValue(flatName, out bucket))
                {
                    bucket = new List<string>();
                    flatToPaths[flatName] = bucket;
                }

                bucket.Add(modulePath);

                entries.Add(new LuaEntry
                {
                    ModulePath = modulePath,
                    FlatName = flatName,
                    SourcePath = files[i],
                });
            }

            List<string> conflicts = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in flatToPaths)
            {
                if (pair.Value.Count > 1)
                {
                    conflicts.Add(pair.Key + "  <-  " + string.Join(", ", pair.Value));
                }
            }

            if (conflicts.Count > 0)
            {
                StringBuilder cb = new StringBuilder();
                cb.Append("扁平化后文件名冲突（共 ").Append(conflicts.Count).Append(" 组）：\n");
                for (int i = 0; i < conflicts.Count; i++)
                {
                    cb.Append("  ").Append(conflicts[i]).Append('\n');
                }

                cb.Append("\n解包后所有脚本放在同一目录，文件名必须全局唯一，请重命名后再打包。");
                summary = cb.ToString();
                Debug.LogError("[LuaBundle] " + summary);
                return false;
            }

            // ---------- 2) 重写 require + 语法检查（全程内存操作） ----------
            // 只替换「确实存在于本工程源码树」的模块：xlua.util / socket.core 这类内置库必须原样保留，
            // 否则会把 require('xlua.util') 改成 require('util')，运行时直接找不到模块。
            Dictionary<string, string> knownModules = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                knownModules[entries[i].ModulePath.Replace('/', '.')] = entries[i].FlatName;
            }

            List<string> syntaxErrors = new List<string>();
            List<string> dynamicWarnings = new List<string>();
            HashSet<string> unknownRequires = new HashSet<string>(StringComparer.Ordinal);
            int rewrittenRefs = 0;

            using (LuaEnv env = new LuaEnv())
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    LuaEntry entry = entries[i];
                    string original = File.ReadAllText(entry.SourcePath, Encoding.UTF8);
                    string rewritten = RewriteRequires(original, entry.ModulePath, knownModules, dynamicWarnings, ref rewrittenRefs, unknownRequires);

                    entry.Source = rewritten;
                    entries[i] = entry;

                    try
                    {
                        using (LuaFunction ignored = env.LoadString(rewritten, entry.FlatName))
                        {
                            // 只验证能否编译
                        }
                    }
                    catch (Exception e)
                    {
                        syntaxErrors.Add(entry.ModulePath + "  ->  " + e.Message);
                    }
                }
            }

            if (unknownRequires.Count > 0)
            {
                Debug.LogWarning(
                    "[LuaBundle] 以下 require 不属于本工程模块（内置库/第三方可忽略；若是拼错的模块名请修正）：\n  "
                    + string.Join("\n  ", new List<string>(unknownRequires).ToArray()));
            }

            if (dynamicWarnings.Count > 0)
            {
                Debug.LogWarning(
                    "[LuaBundle] 检测到动态 require，扁平化后无法解析，运行时可能失败：\n  "
                    + string.Join("\n  ", dynamicWarnings.ToArray()));
            }

            if (syntaxErrors.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Lua 语法检查未通过（共 ").Append(syntaxErrors.Count).Append(" 个文件）：\n");
                for (int i = 0; i < syntaxErrors.Count; i++)
                {
                    sb.Append("  ").Append(syntaxErrors[i]).Append('\n');
                }

                summary = sb.ToString();
                Debug.LogError("[LuaBundle] " + summary);
                return false;
            }

            // ---------- 3) 序列化 ----------
            byte[] buildId = ComputeBuildId(entries);
            byte[] bytes = Serialize(entries, buildId);

            string absBundlePath = Path.Combine(projectRoot, BundleAssetPath);
            string dir = Path.GetDirectoryName(absBundlePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(absBundlePath, bytes);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(BundleAssetPath, ImportAssetOptions.ForceUpdate);

            if (dumpTemp)
            {
                DumpRewritten(projectRoot, entries, buildId);
            }

            summary = string.Format(
                "打包完成：{0} 个脚本，{1:N1} KB\n重写 require {2} 处\nbuildId：{3}\n产物：{4}",
                entries.Count,
                bytes.Length / 1024f,
                rewrittenRefs,
                ToHex(buildId),
                BundleAssetPath);

            Debug.Log("[LuaBundle] " + summary.Replace('\n', ' '));
            return true;
        }

        // ---------------------------------------------------------------- 重写

        /// <summary>
        /// 把 require 的字符串字面量换成扁平模块名，保持引号/括号原样。
        /// 例：<c>require('framework.view_manager')</c> → <c>require('view_manager')</c>
        /// <para>
        /// **只替换 knownModules 里存在的模块**：xlua.util / socket.core 等内置库原样保留，
        /// 否则扁平化会破坏 xLua 自带的 searcher。
        /// </para>
        /// </summary>
        private static string RewriteRequires(
            string source,
            string modulePath,
            Dictionary<string, string> knownModules,
            List<string> dynamicWarnings,
            ref int rewrittenRefs,
            HashSet<string> unknownRequires)
        {
            int count = rewrittenRefs;

            string result = RequireLiteral.Replace(source, match =>
            {
                string head = match.Groups["head"].Value;
                string quote = match.Groups["quote"].Value;
                string module = match.Groups["module"].Value;

                string normalized = module.Replace('\\', '/').Replace('/', '.');

                string flat;
                if (!knownModules.TryGetValue(normalized, out flat))
                {
                    // 不是本工程的模块（内置库 / 第三方 / 拼错的模块名）。
                    // 保持原样，同时记下来让打包日志暴露出来。
                    if (!unknownRequires.Contains(normalized))
                    {
                        unknownRequires.Add(normalized);
                    }

                    return match.Value;
                }

                if (string.Equals(flat, normalized, StringComparison.Ordinal))
                {
                    return match.Value;
                }

                count++;
                return head + quote + flat + quote;
            });

            rewrittenRefs = count;

            Match dynamic = RequireDynamic.Match(result);
            if (dynamic.Success)
            {
                dynamicWarnings.Add(modulePath + "  ->  " + dynamic.Value.Trim());
            }

            return result;
        }

        // ---------------------------------------------------------------- 序列化

        private static byte[] ComputeBuildId(List<LuaEntry> entries)
        {
            using (MD5 md5 = MD5.Create())
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                // 名字 + 内容按名字排序后参与哈希：内容变或增删模块都会变
                List<LuaEntry> sorted = new List<LuaEntry>(entries);
                sorted.Sort((a, b) => string.CompareOrdinal(a.FlatName, b.FlatName));

                for (int i = 0; i < sorted.Count; i++)
                {
                    writer.Write(sorted[i].FlatName);
                    writer.Write('\0');
                    writer.Write(sorted[i].Source);
                    writer.Write('\0');
                }

                writer.Flush();
                return md5.ComputeHash(stream.ToArray());
            }
        }

        private static byte[] Serialize(List<LuaEntry> entries, byte[] buildId)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(LuaBundleReader.CurrentVersion);
                writer.Write(buildId, 0, LuaBundleReader.BuildIdLength);
                writer.Write((uint)entries.Count);

                for (int i = 0; i < entries.Count; i++)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(entries[i].FlatName);
                    byte[] srcBytes = Encoding.UTF8.GetBytes(entries[i].Source);

                    writer.Write((ushort)nameBytes.Length);
                    writer.Write(nameBytes);
                    writer.Write((uint)srcBytes.Length);
                    writer.Write(srcBytes);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        // ---------------------------------------------------------------- 辅助

        private static string ModulePathOf(string absSourceRoot, string absFile)
        {
            string rel = absFile.Substring(absSourceRoot.Length).TrimStart('\\', '/');
            if (rel.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                rel = rel.Substring(0, rel.Length - 4);
            }

            return rel.Replace('\\', '/');
        }

        private static string ToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>把重写后的源码导出到 Library/LuaBundleTemp，便于排查"线上为什么 require 不到"。</summary>
        private static void DumpRewritten(string projectRoot, List<LuaEntry> entries, byte[] buildId)
        {
            try
            {
                string dumpRoot = Path.Combine(projectRoot, DebugDumpFolder);
                if (Directory.Exists(dumpRoot))
                {
                    Directory.Delete(dumpRoot, true);
                }

                Directory.CreateDirectory(dumpRoot);

                for (int i = 0; i < entries.Count; i++)
                {
                    string path = Path.Combine(dumpRoot, entries[i].FlatName + ".lua");
                    File.WriteAllText(path, entries[i].Source, new UTF8Encoding(false));
                }

                File.WriteAllText(
                    Path.Combine(dumpRoot, "build_id.txt"),
                    ToHex(buildId),
                    new UTF8Encoding(false));

                Debug.Log("[LuaBundle] 中间副本已导出: " + DebugDumpFolder);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaBundle] 导出中间副本失败: " + e.Message);
            }
        }
    }
}
