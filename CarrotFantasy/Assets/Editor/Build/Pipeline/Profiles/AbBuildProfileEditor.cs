using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor.BuildPipeline.Profiles
{
    /// <summary>
    /// <see cref="AbBuildProfile"/> 的 Inspector：全部字段用中文标签绘制，
    /// 并提供「刷新」「Dry Run」「开始打包」三个按钮。
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

        private static readonly AbBuildBackend[] BackendValues =
        {
            AbBuildBackend.ScriptableBuildPipeline,
            AbBuildBackend.BuiltIn,
        };

        private static readonly string[] BackendLabels =
        {
            "SBP（Scriptable Build Pipeline）",
            "built-in（BuildPipeline，回退对比用）",
        };

        private static readonly AbBundleLayoutMode[] LayoutValues =
        {
            AbBundleLayoutMode.Meta,
            AbBundleLayoutMode.Code,
        };

        private static readonly string[] LayoutLabels =
        {
            "Meta（按资源 .meta 包名）",
            "Code（当前与 Meta 相同）",
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
            EditorGUILayout.LabelField("构建后端", EditorStyles.boldLabel);
            this.DrawBackendPopup(profile);
            this.DrawLayoutPopup(profile);

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
                    "自动模式：每次打包取「输出目录里当前清单 + 1」，数字不写进这个资产。\n"
                    + "当前将使用版本：" + profile.ResolveManifestVersion(),
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

                // 密码不写进资产：直接读写本机 EditorPrefs，避免随 Git 提交。
                // 密码按主机分 key（dev/prod 不串），且仍然只存本机 EditorPrefs。
                string currentPassword = AssetBundleDeploySettings.GetPassword(profile.UploadHost);
                string password = EditorGUILayout.PasswordField(
                    new GUIContent("SSH 密码（仅本机保存，按主机隔离）"),
                    currentPassword);
                if (password != currentPassword)
                {
                    AssetBundleDeploySettings.SetPassword(profile.UploadHost, password);
                }

                if (GUILayout.Button("测试上传（当前平台目录）", GUILayout.Height(24f)))
                {
                    profile.TestUpload();
                }

                EditorGUILayout.HelpBox(
                    "SSH 密码只保存在本机 EditorPrefs（AB_DeployPassword），不会随资产提交到 Git。\n"
                    + "打包时若勾选了启用云上传，会在结束后自动执行上传步骤。",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("调试", EditorStyles.boldLabel);
            this.DrawProperty("DryRun", "仅试运行（不落盘）");
            this.DrawProperty("SkipSteps", "跳过步骤（调试）", true);

            this.serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("解析结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(profile.Describe(), MessageType.None);

            if (GUILayout.Button(
                    new GUIContent("刷新", "重新读取输出目录里的清单版本"),
                    GUILayout.Height(24f)))
            {
                this.Repaint();
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
                EditorUtility.SetDirty(profile);
            }
        }

        private void DrawBackendPopup(AbBuildProfile profile)
        {
            int index = System.Array.IndexOf(BackendValues, profile.Backend);
            int newIndex = EditorGUILayout.Popup("构建后端", index < 0 ? 0 : index, BackendLabels);
            if (newIndex != index || index < 0)
            {
                Undo.RecordObject(profile, "修改构建后端");
                profile.Backend = BackendValues[newIndex];
                EditorUtility.SetDirty(profile);
            }
        }

        private void DrawLayoutPopup(AbBuildProfile profile)
        {
            int index = System.Array.IndexOf(LayoutValues, profile.Layout);
            int newIndex = EditorGUILayout.Popup("包布局", index < 0 ? 0 : index, LayoutLabels);
            if (newIndex != index || index < 0)
            {
                Undo.RecordObject(profile, "修改包布局");
                profile.Layout = LayoutValues[newIndex];
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
