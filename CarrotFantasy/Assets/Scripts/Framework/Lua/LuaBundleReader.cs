using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CarrotFantasy
{
    /// <summary>
    /// Lua Bundle 运行时解析器。
    /// <para>
    /// 为什么不用「一个脚本一个 TextAsset」：
    ///   1) Unity 默认不导入 .lua，要么加 ScriptedImporter，要么把源码打包成单个资产；
    ///   2) 单资产方案天然支持子目录与同名文件（名字带路径）；
    ///   3) 索引内嵌在包头，不需要额外维护 lua_index.json；
    ///   4) 启动只发一次 AB 请求。
    /// </para>
    /// <para>
    /// 二进制格式（小端，v2）：
    /// <code>
    ///   magic   4B  "CFLB"
    ///   version 2B  uint16（当前 2）
    ///   buildId 16B 内容哈希（用于判断本地是否需要重新解包）
    ///   count   4B  uint32
    ///   重复 count 次：
    ///     nameLen 2B uint16   name UTF8（**扁平模块名**，如 view_manager）
    ///     srcLen  4B uint32   src  UTF8
    /// </code>
    /// </para>
    /// <para>
    /// v2 起只提供**流式**接口：解包时逐条读出并落盘，
    /// 避免把全部源码同时驻留内存（那正是「全量加载」要避免的问题）。
    /// </para>
    /// </summary>
    public static class LuaBundleReader
    {
        /// <summary>AB 包名，与 PrefabABPathPostprocessor 的 LuaScriptBundleName 一致。</summary>
        public const string BundleName = "lua_scripts";

        /// <summary>包内唯一资产名。</summary>
        public const string AssetName = "lua_bundle";

        public const ushort CurrentVersion = 2;

        public const int BuildIdLength = 16;

        private static readonly byte[] Magic = { (byte)'C', (byte)'F', (byte)'L', (byte)'B' };

        /// <summary>把路径形式（framework/class）转成模块名（framework.class）。</summary>
        public static string ToModuleName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').Replace('/', '.');
        }

        /// <summary>取模块名的最后一段（framework.class → class）。</summary>
        public static string ToFlatName(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return string.Empty;
            }

            string normalized = moduleName.Replace('\\', '/').Replace('/', '.');
            int index = normalized.LastIndexOf('.');
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        /// <summary>只读包头，拿到 buildId 与条目数。格式非法时抛异常。</summary>
        public static void ReadHeader(byte[] data, out string buildId, out int count)
        {
            using (BinaryReader reader = OpenReader(data))
            {
                buildId = ReadBuildId(reader);
                count = checked((int)reader.ReadUInt32());
            }
        }

        /// <summary>
        /// 流式遍历条目：每读一条立即回调，读完即丢，不累积。
        /// 解包落盘应使用这个接口。
        /// </summary>
        public static void Enumerate(byte[] data, Action<string, string> onEntry)
        {
            if (onEntry == null)
            {
                throw new ArgumentNullException(nameof(onEntry));
            }

            foreach (KeyValuePair<string, string> entry in EnumerateLazy(data))
            {
                onEntry(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// 流式迭代器版本：调用方可以逐条处理并在需要时分帧（yield return）。
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> EnumerateLazy(byte[] data)
        {
            using (BinaryReader reader = OpenReader(data))
            {
                ReadBuildId(reader);
                int count = checked((int)reader.ReadUInt32());

                for (int i = 0; i < count; i++)
                {
                    string name = ReadString16(reader);
                    string source = ReadString32(reader);
                    yield return new KeyValuePair<string, string>(name, source);
                }
            }
        }

        /// <summary>一次性读成字典（仅工具/调试用；运行时解包请用 Enumerate）。</summary>
        public static int Parse(byte[] data, IDictionary<string, string> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            Enumerate(data, (name, source) => into[name] = source);
            return into.Count;
        }

        // ---------------------------------------------------------------- 内部

        private static BinaryReader OpenReader(byte[] data)
        {
            if (data == null || data.Length < 26)
            {
                throw new InvalidDataException("[LuaBundle] 数据为空或长度不足");
            }

            MemoryStream stream = new MemoryStream(data, writable: false);
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8);

            byte[] magic = reader.ReadBytes(4);
            for (int i = 0; i < magic.Length; i++)
            {
                if (magic[i] != Magic[i])
                {
                    reader.Dispose();
                    throw new InvalidDataException("[LuaBundle] 魔数不匹配，不是有效的 Lua Bundle");
                }
            }

            ushort version = reader.ReadUInt16();
            if (version != CurrentVersion)
            {
                reader.Dispose();
                throw new InvalidDataException(
                    "[LuaBundle] 版本不匹配：包内 " + version + "，客户端期望 " + CurrentVersion
                    + "。请重新执行 Tools/Lua/打包 Lua Bundle。");
            }

            return reader;
        }

        private static string ReadBuildId(BinaryReader reader)
        {
            byte[] raw = reader.ReadBytes(BuildIdLength);
            if (raw.Length != BuildIdLength)
            {
                throw new InvalidDataException("[LuaBundle] buildId 读取不完整");
            }

            StringBuilder sb = new StringBuilder(BuildIdLength * 2);
            for (int i = 0; i < raw.Length; i++)
            {
                sb.Append(raw[i].ToString("x2"));
            }

            return sb.ToString();
        }

        private static string ReadString16(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static string ReadString32(BinaryReader reader)
        {
            int length = checked((int)reader.ReadUInt32());
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }
    }
}
