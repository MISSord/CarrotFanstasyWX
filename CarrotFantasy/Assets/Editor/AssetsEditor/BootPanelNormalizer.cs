using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.EditorTools
{
    /// <summary>
    /// 把已经搭好的 <c>BootPanel.prefab</c> 规范化：
    /// <list type="number">
    /// <item>四个子面板根改小写（Confirm → confirm）；</item>
    /// <item>表里登记的子节点改名为 <c>面板_节点</c> 形式的小写名（Title → confirm_title）；</item>
    /// <item>名称表的键直接等于节点名，不再用斜杠。</item>
    /// </list>
    /// <para>
    /// 为什么键要等于节点名：<see cref="UINameTableDic"/> 以字符串为键，
    /// 键和节点名不一致时，排查"找不到物体"要同时看表和图，容易对不上。
    /// </para>
    /// <para>
    /// 幂等：已经是目标命名时再点一次不会有任何改动。
    /// </para>
    /// </summary>
    public static class BootPanelNormalizer
    {
        private const string PanelPath = "Assets/Resources/AotUI/BootPanel.prefab";

        /// <summary>四个子面板的规范化名字（同时也是它们在名称表里的键）。</summary>
        private static readonly string[] PanelNames = { "confirm", "progress", "error", "fallback" };

        [MenuItem("Tools/UI/规范化启动面板（小写 + 下划线）", priority = 301)]
        private static void Normalize()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "规范化启动面板",
                    "没有找到预制体：\n" + PanelPath + "\n\n请先执行 Tools/UI/生成启动面板（合并 4 个）",
                    "确定");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            int renamedNodes = 0;
            int rewrittenKeys = 0;
            int dropped = 0;
            int renamedOthers = 0;

            try
            {
                UINameTable table = root.GetComponent<UINameTable>();
                if (table == null || table.uiEntries == null || table.uiEntries.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "规范化启动面板",
                        "根节点上没有 UINameTable，或名称表为空。",
                        "确定");
                    return;
                }

                Dictionary<Transform, string> panelRoots = FindPanelRoots(root.transform);

                List<UINameEntry> newEntries = new List<UINameEntry>(table.uiEntries.Count);
                HashSet<string> usedKeys = new HashSet<string>();

                foreach (UINameEntry entry in table.uiEntries)
                {
                    if (entry.uiReference == null)
                    {
                        dropped++;
                        continue;
                    }

                    string newName = ComputeName(entry.uiReference, panelRoots);
                    if (string.IsNullOrEmpty(newName))
                    {
                        dropped++;
                        continue;
                    }

                    if (!string.Equals(entry.uiReference.name, newName))
                    {
                        entry.uiReference.name = newName;
                        renamedNodes++;
                    }

                    if (!usedKeys.Add(newName))
                    {
                        Debug.LogWarning("[BootPanelNormalizer] 键重复，已跳过: " + newName);
                        dropped++;
                        continue;
                    }

                    if (!string.Equals(entry.name, newName))
                    {
                        rewrittenKeys++;
                    }

                    newEntries.Add(new UINameEntry { name = newName, uiReference = entry.uiReference });
                }

                table.uiEntries = newEntries;

                // 第二遍：其余节点（未登记在表里的 panel 容器、文本、图片等）也统一小写，
                // 满足"节点名全小写"的约定。已登记节点此时已符合规则，不受影响。
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == root.transform)
                    {
                        continue;
                    }

                    string lower = transform.name.Trim().ToLowerInvariant().Replace(' ', '_');
                    if (!string.Equals(transform.name, lower))
                    {
                        transform.name = lower;
                        renamedOthers++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder sb = new StringBuilder();
            sb.Append("预制体: ").Append(PanelPath).Append('\n');
            sb.Append("节点改名: ").Append(renamedNodes).Append('\n');
            sb.Append("表键改写: ").Append(rewrittenKeys).Append('\n');
            sb.Append("丢弃条目: ").Append(dropped).Append('\n');
            sb.Append("其它节点改名: ").Append(renamedOthers).Append('\n');
            sb.Append("\n命名规则：键 == 节点名，全小写，用下划线连接（如 confirm_btn_confirm）。");
            Debug.Log("[BootPanelNormalizer]\n" + sb);
            EditorUtility.DisplayDialog("规范化启动面板", sb.ToString(), "确定");
        }

        /// <summary>找出四个子面板根，映射为 根节点 → 规范名。</summary>
        private static Dictionary<Transform, string> FindPanelRoots(Transform root)
        {
            Dictionary<Transform, string> result = new Dictionary<Transform, string>();

            foreach (string panelName in PanelNames)
            {
                Transform found = null;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (string.Equals(child.name.Trim().ToLowerInvariant(), panelName))
                    {
                        found = child;
                        break;
                    }
                }

                if (found == null)
                {
                    Debug.LogWarning("[BootPanelNormalizer] 找不到子面板: " + panelName);
                    continue;
                }

                if (!string.Equals(found.name, panelName))
                {
                    found.name = panelName;
                }

                result[found] = panelName;
            }

            return result;
        }

        /// <summary>
        /// 计算某个节点应有的名字：<c>面板_节点</c>（小写、下划线）。
        /// 面板根自身直接叫面板名；节点名已包含面板前缀时去掉重复（ProgressFill → progress_fill）。
        /// </summary>
        private static string ComputeName(GameObject go, Dictionary<Transform, string> panelRoots)
        {
            string panel = null;
            Transform cursor = go.transform;

            while (cursor != null)
            {
                string mapped;
                if (panelRoots.TryGetValue(cursor, out mapped))
                {
                    panel = mapped;
                    break;
                }

                cursor = cursor.parent;
            }

            string leaf = go.name.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrEmpty(panel))
            {
                // 不在任何面板下（例如根节点本身），保持小写原名。
                return leaf;
            }

            if (string.Equals(go.transform, cursor))
            {
                return panel;
            }

            if (leaf.StartsWith(panel))
            {
                string stripped = leaf.Substring(panel.Length).TrimStart('_');
                if (stripped.Length > 0)
                {
                    leaf = stripped;
                }
            }

            return panel + "_" + leaf;
        }
    }
}
