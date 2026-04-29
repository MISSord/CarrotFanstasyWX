using UnityEditor;
using UnityEngine;

public static class ToggleSelectedActiveEditor
{
    [MenuItem("Tools/切换选中节点激活状态 _SPACE")]
    private static void ToggleSelectedActive()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || EditorUtility.IsPersistent(selected))
        {
            return;
        }

        bool nextActive = !selected.activeSelf;
        Undo.RecordObject(selected, "Toggle GameObject Active");
        selected.SetActive(nextActive);
        EditorUtility.SetDirty(selected);
    }

    [MenuItem("Tools/切换选中节点激活状态 _SPACE", true)]
    private static bool ValidateToggleSelectedActive()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null && !EditorUtility.IsPersistent(selected);
    }
}
