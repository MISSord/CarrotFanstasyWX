using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// <see cref="AbBuildProfile"/> 的 Inspector：全部字段用中文标签绘制，
    /// 并提供「重新探测版本」「Dry Run」「开始打包」三个按钮。
    /// <para>
    /// 注意：本文件的命名空间含 <c>Editor</c> 段，基类必须写全限定
    /// <see cref="UnityEditor.Editor"/>，否则 `Editor` 会被解析成命名空间（CS0118）。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AbBuildProfile))]
    public sealed class AbBuildProfileEditor : UnityEditor.Editor
    {
        private static readonly BuildTarget[] SupportedTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
        };

        private static readonly string[] SupportedTargetLabels =
        {
            "Windows 64 位",
            "macOS",
        };

        private static readonly CompressionType[] CompressionValues =
        {
            CompressionType.StandardCompression,
            CompressionType.ChunkBasedCompression,
            CompressionType.NoCompression,
        };

        private static readonly string[] CompressionLabels =
        {
            "标准压缩（LZMA，体积最小）",
            "分块压缩（LZ4，推荐）",
            "不压缩（体积最大）",
        };

        public override void OnInspectorGUI()
        {
            var profile = (AbBuildProfile)this.target;

            this.serializedObject.Update();

            EditorGUILayout.LabelField("平台与输出", EditorStyles.boldLabel);
            this.DrawTargetPopup(profile);
            this.DrawProperty("OutputRoot", "输出根目录");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("压缩", EditorStyles.boldLabel);
            this.DrawCompressionPopup(profile);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("清单版本", EditorStyles.boldLabel);
            this.DrawProperty("AutoManifestVersion", "自动取下一版本");
            using (new EditorGUI.DisabledGroupScope(profile.AutoManifestVersion))
            {
                this.DrawProperty("ManifestVersion", "清单版本");
            }

            if (profile.AutoManifestVersion)
            {
                EditorGUILayout.HelpBox(
                    "自动模式：每次打包取「输出目录里当前清单 + 1」，改平台/输出目录后会自动刷新。\n"
                    + "当前将使用版本：" + profile.ManifestVersion,
                    MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("开关", EditorStyles.boldLabel);
            this.DrawProperty("ClearOutputFolder", "清空平台输出目录");
            this.DrawProperty("CopyToStreamingAssets", "拷贝到 StreamingAssets");
            this.DrawProperty("ForceRebuild", "强制重新构建（忽略增量）");
            this.DrawProperty("PackAtlasesBeforeBuild", "构建前打包 UI 图集");
            this.DrawProperty("ShowManifestDialog", "打包完成弹窗提示");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("云上传（SFTP）", EditorStyles.boldLabel);
            this.DrawProperty("EnableUpload", "启用云上传");
            if (profile.EnableUpload)
            {
                EditorGUI.indentLevel++;
                this.DrawProperty("UploadHost", "服务器地址");
                this.DrawProperty("UploadPort", "端口");
                this.DrawProperty("UploadUser", "用户名");
                this.DrawProperty("UploadRemotePath", "远程目录");
                this.DrawProperty("UploadUsePrivateKey", "使用私钥登录");
                if (profile.UploadUsePrivateKey)
                {
                    this.DrawProperty("UploadPrivateKeyPath", "私钥文件");
                }

                EditorGUILayout.HelpBox(
                    "SSH 密码不保存在本资产（避免随 Git 提交），只读取本机 EditorPrefs 的 AB_DeployPassword。\n"
                    + "首次使用请到 AB Packager 窗口的「云服务器上传」里填写一次密码。",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("流程", EditorStyles.boldLabel);
            this.DrawProperty("UseStepPipeline", "使用步骤执行器");
            this.DrawProperty("DryRun", "仅试运行（不落盘）");
            this.DrawProperty("SkipSteps", "跳过步骤（调试）", true);

            this.serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("解析结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(profile.Describe(), MessageType.None);

            if (GUILayout.Button("重新探测清单版本", GUILayout.Height(24f)))
            {
                profile.RefreshManifestVersion();
                EditorUtility.SetDirty(profile);
            }

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

        private void DrawProperty(string propertyName, string label, bool includeChildren = false)
        {
            SerializedProperty property = this.serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, "（字段缺失）");
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }

        private void DrawTargetPopup(AbBuildProfile profile)
        {
            int index = System.Array.IndexOf(SupportedTargets, profile.BuildTarget);
            int newIndex = EditorGUILayout.Popup("目标平台", index < 0 ? 0 : index, SupportedTargetLabels);
            if (newIndex != index || index < 0)
            {
                Undo.RecordObject(profile, "修改打包平台");
                profile.BuildTarget = SupportedTargets[newIndex];
                profile.RefreshManifestVersion();
                EditorUtility.SetDirty(profile);
            }
        }

        private void DrawCompressionPopup(AbBuildProfile profile)
        {
            int index = System.Array.IndexOf(CompressionValues, profile.Compression);
            int newIndex = EditorGUILayout.Popup("压缩格式", index < 0 ? 1 : index, CompressionLabels);
            if (newIndex != index || index < 0)
            {
                Undo.RecordObject(profile, "修改压缩格式");
                profile.Compression = CompressionValues[newIndex];
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
