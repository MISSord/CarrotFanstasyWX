using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PrefabReferenceFinderWindow : EditorWindow
{
    private const string MenuPath = "Tools/资源工具/Prefab 引用查找";

    private Object _targetAsset;
    private readonly List<string> _matchedPrefabPaths = new List<string>();
    private Vector2 _scrollPos;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<PrefabReferenceFinderWindow>("Prefab 引用查找");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("选中任意资源，查找被哪些预制体引用", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _targetAsset = EditorGUILayout.ObjectField("目标资源", _targetAsset, typeof(Object), false);
        if (GUILayout.Button("使用当前选中", GUILayout.Width(110f)))
        {
            _targetAsset = Selection.activeObject;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(_targetAsset == null))
        {
            if (GUILayout.Button("开始查找", GUILayout.Height(28f)))
            {
                FindReferences();
            }
        }
        if (GUILayout.Button("清空结果", GUILayout.Height(28f), GUILayout.Width(110f)))
        {
            _matchedPrefabPaths.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"匹配预制体数量: {_matchedPrefabPaths.Count}");

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        for (int i = 0; i < _matchedPrefabPaths.Count; i++)
        {
            var prefabPath = _matchedPrefabPaths[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i + 1).ToString(), GUILayout.Width(30f));
            EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
            if (GUILayout.Button("定位", GUILayout.Width(60f)))
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void FindReferences()
    {
        _matchedPrefabPaths.Clear();

        if (_targetAsset == null)
        {
            ShowNotification(new GUIContent("请先选择目标资源"));
            return;
        }

        string targetPath = AssetDatabase.GetAssetPath(_targetAsset);
        if (string.IsNullOrEmpty(targetPath))
        {
            ShowNotification(new GUIContent("目标资源路径无效"));
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int total = prefabGuids.Length;

        for (int i = 0; i < total; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            float progress = total == 0 ? 1f : (i + 1f) / total;

            bool canceled = EditorUtility.DisplayCancelableProgressBar(
                "扫描预制体引用",
                $"正在检查: {prefabPath}",
                progress);

            if (canceled)
            {
                break;
            }

            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            for (int j = 0; j < dependencies.Length; j++)
            {
                if (dependencies[j] == targetPath)
                {
                    _matchedPrefabPaths.Add(prefabPath);
                    break;
                }
            }
        }

        EditorUtility.ClearProgressBar();

        if (_matchedPrefabPaths.Count == 0)
        {
            ShowNotification(new GUIContent("未找到引用该资源的预制体"));
        }
        else
        {
            ShowNotification(new GUIContent($"查找完成，共 {_matchedPrefabPaths.Count} 个"));
        }
    }
}
