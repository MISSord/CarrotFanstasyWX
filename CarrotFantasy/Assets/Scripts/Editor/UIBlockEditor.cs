#if UNITY_EDITOR
using CarrotFantasy;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor
{
    [CustomEditor(typeof(UIBlock), true)]
    [CanEditMultipleObjects]
    public class UIBlockEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
