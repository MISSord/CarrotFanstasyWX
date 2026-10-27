using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(UINameEntry))]
public class UINameEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 获取序列化属性
        SerializedProperty nameProp = property.FindPropertyRelative("name");
        SerializedProperty refProp = property.FindPropertyRelative("uiReference");

        // 设置UI高度
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;

        // 计算各元素位置
        Rect refRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect nameRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

        // 绘制引用字段
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(refRect, refProp, GUIContent.none);
        if (EditorGUI.EndChangeCheck())
        {
            // 当引用改变时自动更新名称
            GameObject uiObject = refProp.objectReferenceValue as GameObject;
            if (uiObject != null)
            {
                nameProp.stringValue = uiObject.name;
            }
        }

        // 绘制名称字段
        //EditorGUI.BeginDisabledGroup(true);
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("UI名称"));
        //EditorGUI.EndDisabledGroup();

        // 添加拖放区域
        HandleDragAndDrop(refRect, nameProp, refProp);
    }

    private void HandleDragAndDrop(Rect dropArea, SerializedProperty nameProp, SerializedProperty refProp)
    {
        Event currentEvent = Event.current;

        if (!dropArea.Contains(currentEvent.mousePosition))
            return;

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
                // 验证拖拽对象是否有效
                bool isValid = IsValidDragObject();
                DragAndDrop.visualMode = isValid ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
                break;

            case EventType.DragPerform:
                DragAndDrop.AcceptDrag();

                // 处理拖放的对象
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is GameObject gameObject)
                    {
                        refProp.objectReferenceValue = gameObject;
                        nameProp.stringValue = gameObject.name;
                        break; // 只处理第一个有效对象
                    }
                }

                // 应用修改
                refProp.serializedObject.ApplyModifiedProperties();
                currentEvent.Use();
                break;

            case EventType.Repaint:
                // 添加拖放区域的视觉反馈
                if (dropArea.Contains(currentEvent.mousePosition))
                {
                    EditorGUI.DrawRect(dropArea, new Color(0.3f, 0.6f, 1f, 0.2f));
                }
                break;
        }
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

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2 + 4; // 两个字段高度 + 间距
    }
}
