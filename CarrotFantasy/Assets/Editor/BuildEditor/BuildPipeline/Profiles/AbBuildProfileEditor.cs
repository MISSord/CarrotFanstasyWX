using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// <see cref="AbBuildProfile"/> 的 Inspector：显示解析后的路径/后端/步骤数，
    /// 并提供「Dry Run」与「开始打包」两个按钮。
    /// </summary>
    [CustomEditor(typeof(AbBuildProfile))]
    public sealed class AbBuildProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();

            var profile = (AbBuildProfile)this.target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("解析结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(profile.Describe(), MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run（只打印步骤）", GUILayout.Height(28f)))
                {
                    profile.DryRunOnce();
                }

                if (GUILayout.Button("开始打包", GUILayout.Height(28f)))
                {
                    profile.Build();
                }
            }
        }
    }
}
