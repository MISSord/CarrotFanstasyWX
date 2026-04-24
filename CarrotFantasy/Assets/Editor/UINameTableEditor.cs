using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(UINameTable))]
public class UINameTableEditor : Editor
{
    private SerializedProperty uiEntriesProp;

    private void OnEnable()
    {
        uiEntriesProp = serializedObject.FindProperty("uiEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("拖拽UI元素到下方字段区域，将自动使用UI元素名称填充名称字段", MessageType.Info);
        EditorGUILayout.Space();

        // 显示列表
        EditorGUILayout.PropertyField(uiEntriesProp, true);

        // 添加列表控制按钮
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("添加新条目"))
        {
            uiEntriesProp.arraySize++;
        }

        if (GUILayout.Button("清空列表"))
        {
            uiEntriesProp.ClearArray();
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();

        // 添加额外的拖放区域
        AddExtraDropArea();
    }

    private void AddExtraDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "拖拽UI元素到此处批量添加", EditorStyles.helpBox);

        Event currentEvent = Event.current;

        if (!dropArea.Contains(currentEvent.mousePosition))
            return;

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                bool isValid = IsValidDragObject();
                DragAndDrop.visualMode = isValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
                break;

            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();

                // 批量添加拖拽的UI元素
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is GameObject gameObject)
                    {
                        AddUIEntry(gameObject);
                    }
                }

                serializedObject.ApplyModifiedProperties();
                currentEvent.Use();
                break;

            case EventType.Repaint:
                if (dropArea.Contains(currentEvent.mousePosition))
                {
                    EditorGUI.DrawRect(dropArea, new Color(0.2f, 0.8f, 0.3f, 0.3f));
                }
                break;
        }
    }

    private void AddUIEntry(GameObject uiObject)
    {
        for(int i = 0; i < uiEntriesProp.arraySize; i++)
        {
            SerializedProperty curEntry = uiEntriesProp.GetArrayElementAtIndex(i);
            if (curEntry.FindPropertyRelative("uiReference").objectReferenceValue == uiObject)
                return;
        }
        int index = uiEntriesProp.arraySize;
        uiEntriesProp.arraySize++;

        SerializedProperty newEntry = uiEntriesProp.GetArrayElementAtIndex(index);
        newEntry.FindPropertyRelative("name").stringValue = uiObject.name;
        newEntry.FindPropertyRelative("uiReference").objectReferenceValue = uiObject;
    }

    private bool IsValidDragObject()
    {
        if (DragAndDrop.objectReferences.Length == 0)
            return false;

        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is GameObject)
                return true;
        }

        return false;
    }
}
