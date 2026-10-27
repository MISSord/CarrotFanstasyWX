using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UINameTable))]
public class UINameTableEditor : Editor
{
    private static readonly Color ListHighlightColor = new Color(0.2f, 0.75f, 0.35f, 0.85f);
    private static readonly Color ListNormalColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);

    private SerializedProperty uiEntriesProp;
    private readonly HashSet<int> highlightedIndices = new HashSet<int>();

    private void OnEnable()
    {
        uiEntriesProp = serializedObject.FindProperty("uiEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        this.RefreshDragHighlights();
        this.HandleInspectorDragUpdated();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "\u62d6\u62fd UI \u5230\u4e0b\u65b9\u533a\u57df\u53ef\u6dfb\u52a0\uff1b\u82e5\u5df2\u5728\u5217\u8868\u4e2d\uff0c\u5bf9\u5e94\u6761\u76ee\u4f1a\u7eff\u8272\u9ad8\u4eae\u3002",
            MessageType.Info);
        EditorGUILayout.Space();

        this.DrawEntriesList();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("\u6309\u540d\u79f0\u6392\u5e8f"))
        {
            this.SortEntriesByName();
        }

        if (GUILayout.Button("\u6e05\u7a7a\u5217\u8868"))
        {
            uiEntriesProp.ClearArray();
            this.highlightedIndices.Clear();
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();

        this.DrawDropArea();

        if (this.IsDraggingGameObject())
        {
            this.Repaint();
        }
    }

    private void DrawEntriesList()
    {
        EditorGUILayout.LabelField(
            "UI Entries (" + uiEntriesProp.arraySize + ")",
            EditorStyles.boldLabel);

        for (int i = 0; i < uiEntriesProp.arraySize; i++)
        {
            SerializedProperty entry = uiEntriesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = entry.FindPropertyRelative("name");
            SerializedProperty refProp = entry.FindPropertyRelative("uiReference");
            bool highlight = this.highlightedIndices.Contains(i);

            // ???????????????????????????????????????????????????
            Rect rowRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, highlight ? ListHighlightColor : ListNormalColor);
            }

            float x = rowRect.x + 4f;
            float y = rowRect.y + 2f;
            float h = rowRect.height - 4f;

            EditorGUI.LabelField(new Rect(x, y, 32f, h), "[" + i + "]");
            x += 34f;

            float nameWidth = Mathf.Max(80f, (rowRect.width - 34f - 28f) * 0.35f);
            float refWidth = rowRect.width - 34f - 28f - nameWidth - 8f;

            EditorGUI.PropertyField(new Rect(x, y, nameWidth, h), nameProp, GUIContent.none);
            x += nameWidth + 4f;
            EditorGUI.PropertyField(new Rect(x, y, refWidth, h), refProp, GUIContent.none);
            x += refWidth + 4f;

            if (GUI.Button(new Rect(x, y, 22f, h), "X"))
            {
                uiEntriesProp.DeleteArrayElementAtIndex(i);
                break;
            }
        }
    }

    private void RefreshDragHighlights()
    {
        this.highlightedIndices.Clear();
        if (!this.IsDraggingGameObject() || uiEntriesProp == null)
        {
            return;
        }

        Object[] dragged = DragAndDrop.objectReferences;
        for (int d = 0; d < dragged.Length; d++)
        {
            GameObject go = dragged[d] as GameObject;
            if (go == null)
            {
                continue;
            }

            for (int i = 0; i < uiEntriesProp.arraySize; i++)
            {
                Object existing = uiEntriesProp
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("uiReference")
                    .objectReferenceValue;
                if (existing == go)
                {
                    this.highlightedIndices.Add(i);
                }
            }
        }
    }

    private void HandleInspectorDragUpdated()
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.DragUpdated || !this.IsDraggingGameObject())
        {
            return;
        }

        // ?????? Inspector ????????????????????????????????????????
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        currentEvent.Use();
    }

    private void DrawDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        string dropLabel = this.highlightedIndices.Count > 0
            ? "\u62d6\u5230\u6b64\u5904\u6dfb\u52a0\uff08\u7eff\u8272\u884c\u4e3a\u5217\u8868\u4e2d\u5df2\u5b58\u5728\u9879\uff09"
            : "\u62d6\u62fd UI \u5143\u7d20\u5230\u6b64\u5904\u6279\u91cf\u6dfb\u52a0";
        GUI.Box(dropArea, dropLabel, EditorStyles.helpBox);

        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition))
        {
            return;
        }

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                DragAndDrop.visualMode = this.IsDraggingGameObject()
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
                break;

            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    GameObject gameObject = draggedObject as GameObject;
                    if (gameObject != null)
                    {
                        this.AddUIEntry(gameObject);
                    }
                }

                serializedObject.ApplyModifiedProperties();
                currentEvent.Use();
                break;
        }
    }

    private void AddUIEntry(GameObject uiObject)
    {
        for (int i = 0; i < uiEntriesProp.arraySize; i++)
        {
            SerializedProperty curEntry = uiEntriesProp.GetArrayElementAtIndex(i);
            if (curEntry.FindPropertyRelative("uiReference").objectReferenceValue == uiObject)
            {
                return;
            }
        }

        int index = uiEntriesProp.arraySize;
        uiEntriesProp.arraySize++;

        SerializedProperty newEntry = uiEntriesProp.GetArrayElementAtIndex(index);
        newEntry.FindPropertyRelative("name").stringValue = uiObject.name;
        newEntry.FindPropertyRelative("uiReference").objectReferenceValue = uiObject;
    }

    private void SortEntriesByName()
    {
        UINameTable table = (UINameTable)this.target;
        if (table.uiEntries == null || table.uiEntries.Count <= 1)
        {
            return;
        }

        Undo.RecordObject(table, "Sort UINameTable by Name");

        List<UINameEntry> sorted = new List<UINameEntry>(table.uiEntries);
        sorted.Sort((a, b) =>
        {
            string nameA = a.name ?? string.Empty;
            string nameB = b.name ?? string.Empty;
            return string.CompareOrdinal(nameA, nameB);
        });

        table.uiEntries = sorted;
        EditorUtility.SetDirty(table);
        serializedObject.Update();
        this.highlightedIndices.Clear();
    }

    private bool IsDraggingGameObject()
    {
        Object[] refs = DragAndDrop.objectReferences;
        if (refs == null || refs.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < refs.Length; i++)
        {
            if (refs[i] is GameObject)
            {
                return true;
            }
        }

        return false;
    }
}
