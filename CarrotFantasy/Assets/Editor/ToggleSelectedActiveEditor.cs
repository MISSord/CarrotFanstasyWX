using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class ToggleSelectedActiveEditor
{
    [MenuItem("Tools/切换选中节点激活状态 _SPACE")]
    private static void ToggleSelectedActive()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        List<GameObject> validObjects = new List<GameObject>();
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject target = selectedObjects[i];
            if (target != null && !EditorUtility.IsPersistent(target))
            {
                validObjects.Add(target);
            }
        }

        if (validObjects.Count == 0)
        {
            return;
        }

        Undo.RecordObjects(validObjects.ToArray(), "Toggle GameObjects Active");
        for (int i = 0; i < validObjects.Count; i++)
        {
            GameObject target = validObjects[i];
            target.SetActive(!target.activeSelf);
            EditorUtility.SetDirty(target);
        }
    }

    [MenuItem("Tools/切换选中节点激活状态 _SPACE", true)]
    private static bool ValidateToggleSelectedActive()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject target = selectedObjects[i];
            if (target != null && !EditorUtility.IsPersistent(target))
            {
                return true;
            }
        }

        return false;
    }
}
