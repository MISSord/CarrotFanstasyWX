using System.Collections.Generic;
using System.Text;

namespace Luban
{
    /// <summary>
    /// Luban 生成代码 ToString 使用的集合格式化工具（cs-simple-json 模板依赖）。
    /// </summary>
    public static class StringUtil
    {
        public static string CollectionToString<T>(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (T item in collection)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                sb.Append(item);
            }
            sb.Append(']');
            return sb.ToString();
        }

        public static string CollectionToString<TK, TV>(IDictionary<TK, TV> dic)
        {
            if (dic == null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<TK, TV> kv in dic)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                sb.Append(kv.Key);
                sb.Append(':');
                sb.Append(kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }
    }
}
