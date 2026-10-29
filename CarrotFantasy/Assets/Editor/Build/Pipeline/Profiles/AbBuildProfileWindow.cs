using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// 打包界面：直接画出 <see cref="AbBuildProfile"/> 的检查器，不用先在 Project 里翻资产。
    /// </summary>
    public sealed class AbBuildProfileWindow : EditorWindow
    {
        private const string MenuPath = "Tools/AssetBundle/打开打包界面";
        private const string SelectedPathKey = "AB_BuildProfileWindow_Path";

        private AbBuildProfile[] profiles = Array.Empty<AbBuildProfile>();
        private string[] profileNames = Array.Empty<string>();
        private int selectedIndex;
        private UnityEditor.Editor profileEditor;
        private Vector2 scroll;

        [MenuItem(MenuPath, false, 300)]
        public static void Open()
        {
            AbBuildProfileWindow window = GetWindow<AbBuildProfileWindow>("AB 打包");
            window.minSize = new Vector2(460f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            this.ReloadProfiles();
        }

        private void OnDisable()
        {
            this.DestroyProfileEditor();
        }

        private void OnGUI()
        {
            if (this.profiles.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "还没有 AB 打包配置。用 Create → CarrotFantasy → Build → AB 打包配置 新建一份。",
                    MessageType.Info);
                if (GUILayout.Button("重新查找"))
                {
                    this.ReloadProfiles();
                }

                return;
            }

            int nextIndex = GUILayout.Toolbar(this.selectedIndex, this.profileNames);
            if (nextIndex != this.selectedIndex)
            {
                this.selectedIndex = nextIndex;
                this.RememberSelection();
                this.BindProfileEditor();
            }

            EditorGUILayout.Space(4f);
            this.scroll = EditorGUILayout.BeginScrollView(this.scroll);
            if (this.profileEditor != null)
            {
                this.profileEditor.OnInspectorGUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ReloadProfiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:AbBuildProfile");
            var found = new System.Collections.Generic.List<AbBuildProfile>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AbBuildProfile profile = AssetDatabase.LoadAssetAtPath<AbBuildProfile>(path);
                if (profile != null)
                {
                    found.Add(profile);
                }
            }

            found.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
            this.profiles = found.ToArray();
            this.profileNames = new string[this.profiles.Length];
            for (int i = 0; i < this.profiles.Length; i++)
            {
                this.profileNames[i] = this.profiles[i].name;
            }

            string savedPath = EditorPrefs.GetString(SelectedPathKey, string.Empty);
            this.selectedIndex = 0;
            for (int i = 0; i < this.profiles.Length; i++)
            {
                if (AssetDatabase.GetAssetPath(this.profiles[i]) == savedPath)
                {
                    this.selectedIndex = i;
                    break;
                }
            }

            this.BindProfileEditor();
        }

        private void RememberSelection()
        {
            if (this.selectedIndex < 0 || this.selectedIndex >= this.profiles.Length)
            {
                return;
            }

            EditorPrefs.SetString(SelectedPathKey, AssetDatabase.GetAssetPath(this.profiles[this.selectedIndex]));
        }

        private void BindProfileEditor()
        {
            this.DestroyProfileEditor();
            if (this.selectedIndex < 0 || this.selectedIndex >= this.profiles.Length)
            {
                return;
            }

            this.profileEditor = UnityEditor.Editor.CreateEditor(this.profiles[this.selectedIndex]);
        }

        private void DestroyProfileEditor()
        {
            if (this.profileEditor == null)
            {
                return;
            }

            DestroyImmediate(this.profileEditor);
            this.profileEditor = null;
        }
    }

    /// <summary>
    /// 在 Unity 主工具栏右侧放一个「打包」按钮，点开 <see cref="AbBuildProfileWindow"/>。
    /// Unity 2023 的主工具栏没有公开插入点，所以挂到内部 Toolbar 的右侧区域。
    /// </summary>
    [InitializeOnLoad]
    static class AbBuildToolbarButton
    {
        private const string ZoneName = "ToolbarZoneRightAlign";
        private const string ButtonName = "CarrotFantasyAbBuildToolbarButton";

        private static Button attachedButton;

        static AbBuildToolbarButton()
        {
            EditorApplication.update += TryAttach;
        }

        private static void TryAttach()
        {
            if (attachedButton != null && attachedButton.panel != null)
            {
                return;
            }

            Type toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
            {
                return;
            }

            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars == null || toolbars.Length == 0)
            {
                return;
            }

            FieldInfo rootField = toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            VisualElement root = rootField != null ? rootField.GetValue(toolbars[0]) as VisualElement : null;
            if (root == null)
            {
                return;
            }

            VisualElement zone = root.Q(ZoneName);
            if (zone == null)
            {
                return;
            }

            if (zone.Q(ButtonName) is Button existingButton)
            {
                attachedButton = existingButton;
                return;
            }

            var button = new Button(AbBuildProfileWindow.Open)
            {
                name = ButtonName,
                text = "打包",
                tooltip = "打开 AB 打包界面",
            };
            button.style.height = 20f;
            button.style.marginTop = 1f;
            button.style.marginRight = 6f;
            button.style.paddingLeft = 8f;
            button.style.paddingRight = 8f;
            zone.Insert(0, button);
            attachedButton = button;
        }
    }
}
