using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy.EditorTools
{
    /// <summary>
    /// 把启动期四个独立面板合并成一个 <c>BootPanel</c> 预制体，
    /// 供 <see cref="AotBootPanel"/> 通过"切节点"的方式复用。
    /// <para>
    /// 为什么需要这个工具：<see cref="UINameTableDic"/> 以名字为键，重名会报错丢弃，
    /// 而四个面板共有 4 个 Title、3 个 Btn_Confirm。直接嵌套会导致只有第一个面板能被找到。
    /// 本工具给每个子面板的节点键名加上 <c>Confirm/</c> 这类前缀来解决冲突。
    /// </para>
    /// <para>
    /// 生成结果：<c>Assets/Resources/AotUI/BootPanel.prefab</c>，子节点为
    /// <c>Confirm</c> / <c>Progress</c> / <c>Error</c> / <c>Fallback</c>（保留为嵌套预制体实例，
    /// 后续改原预制体仍会同步过来）。
    /// </para>
    /// </summary>
    public static class BootPanelMerger
    {
        private const string AotUiFolder = "Assets/Resources/AotUI";
        private const string OutputPath = AotUiFolder + "/BootPanel.prefab";

        /// <summary>子面板名 → 原预制体路径。</summary>
        private static readonly (string Name, string PrefabPath)[] Sources =
        {
            ("Confirm", AotUiFolder + "/DownloadConfirm.prefab"),
            ("Progress", AotUiFolder + "/DownloadProgress.prefab"),
            ("Error", AotUiFolder + "/UpdateListError.prefab"),
            ("Fallback", AotUiFolder + "/UpdateListFallback.prefab"),
        };

        [MenuItem("Tools/UI/生成启动面板（合并 4 个）", priority = 300)]
        private static void Merge()
        {
            List<string> missing = new List<string>();
            foreach ((string _, string path) in Sources)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    missing.Add(path);
                }
            }

            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "生成启动面板",
                    "以下预制体不存在，已中止：\n\n" + string.Join("\n", missing),
                    "确定");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath) != null;
            if (exists && !EditorUtility.DisplayDialog(
                    "生成启动面板",
                    "已存在 BootPanel.prefab，是否覆盖？\n\n" + OutputPath,
                    "覆盖", "取消"))
            {
                return;
            }

            GameObject root = BuildRoot(out List<UINameEntry> entries);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder sb = new StringBuilder();
            sb.Append("已生成: ").Append(OutputPath).Append('\n');
            sb.Append("键总数: ").Append(entries.Count).Append('\n');
            sb.Append("子节点: ");
            for (int i = 0; i < Sources.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(" / ");
                }

                sb.Append(Sources[i].Name);
            }

            sb.Append("\n\n下一步：运行游戏，确认四个面板显示与按钮正常；\n确认无误后再删除旧的四个 View 类与预制体。");
            Debug.Log("[BootPanelMerger]\n" + sb);
            EditorUtility.DisplayDialog("生成启动面板", sb.ToString(), "确定");
        }

        private static GameObject BuildRoot(out List<UINameEntry> entries)
        {
            entries = new List<UINameEntry>();

            GameObject root = new GameObject("BootPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1960f, 1280f);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1960f, 1280f);
            scaler.matchWidthOrHeight = 0.5f;

            foreach ((string name, string prefabPath) in Sources)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    Debug.LogError("[BootPanelMerger] 实例化失败: " + prefabPath);
                    continue;
                }

                instance.name = name;
                instance.transform.SetParent(root.transform, false);

                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }

                // 子面板自己就是 Canvas 时，避免它抢排序 / 重复缩放。
                Canvas childCanvas = instance.GetComponent<Canvas>();
                if (childCanvas != null)
                {
                    childCanvas.overrideSorting = false;
                }

                // 子根本身也登记，便于运行时 SetActive 切换。
                entries.Add(new UINameEntry { name = name, uiReference = instance });

                CollectEntries(instance, name, entries);
            }

            UINameTable table = root.AddComponent<UINameTable>();
            table.uiEntries = entries;
            return root;
        }

        /// <summary>把某个子面板原有名称表里的条目，加上面板前缀后复制到合并表。</summary>
        private static void CollectEntries(GameObject panelRoot, string prefix, List<UINameEntry> output)
        {
            UINameTable[] tables = panelRoot.GetComponentsInChildren<UINameTable>(true);
            if (tables.Length == 0)
            {
                Debug.LogWarning("[BootPanelMerger] 子面板没有 UINameTable，将无法按名字取节点: " + prefix);
                return;
            }

            foreach (UINameTable table in tables)
            {
                if (table.uiEntries == null)
                {
                    continue;
                }

                foreach (UINameEntry entry in table.uiEntries)
                {
                    if (string.IsNullOrEmpty(entry.name) || entry.uiReference == null)
                    {
                        continue;
                    }

                    string key = prefix + "/" + entry.name;
                    if (output.Exists(e => e.name == key))
                    {
                        Debug.LogWarning("[BootPanelMerger] 键重复，已跳过: " + key);
                        continue;
                    }

                    output.Add(new UINameEntry { name = key, uiReference = entry.uiReference });
                }
            }
        }
    }
}
