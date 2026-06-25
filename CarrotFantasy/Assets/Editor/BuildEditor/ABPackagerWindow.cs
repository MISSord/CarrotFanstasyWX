using System.IO;
using UnityEditor;
using UnityEngine;

public class ABPackagerWindow : EditorWindow
{
    private string outputPath = AssetBundleBuildSettings.DefaultOutputRoot;
    private BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
    private CompressionType compressionType = CompressionType.ChunkBasedCompression;
    private bool clearFolders = true;
    private bool copyToStreamingAssets = false;
    private bool packAtlasesBeforeBuild = true;
    private string cdnUrlTemplate = string.Empty;
    private int lastVersionNumber;
    private int curVersionNumber;

    private Vector2 scrollPosition;
    private bool showAdvancedSettings;
    private CustomManifest generatedManifest;

    [MenuItem("Tools/打开AssetBundle打包窗口")]
    public static void ShowWindow()
    {
        ABPackagerWindow window = GetWindow<ABPackagerWindow>("AB Packager");
        window.minSize = new Vector2(420, 520);
        window.LoadFromSettings();
        window.Show();
    }

    void LoadFromSettings()
    {
        outputPath = AssetBundleBuildSettings.GetOutputRoot();
        buildTarget = AssetBundleBuildSettings.GetBuildTarget();
        compressionType = AssetBundleBuildSettings.GetCompressionType();
        cdnUrlTemplate = AssetBundleBuildSettings.GetCdnUrlTemplate();
        lastVersionNumber = AssetBundleBuildSettings.ReadLastManifestVersion(outputPath, buildTarget);
        curVersionNumber = AssetBundleBuildSettings.SuggestNextManifestVersion(outputPath, buildTarget);
    }

    void OnDestroy()
    {
        AssetBundleBuildSettings.SetOutputRoot(outputPath);
        AssetBundleBuildSettings.SetBuildTarget(buildTarget);
        AssetBundleBuildSettings.SetCompressionType(compressionType);
        AssetBundleBuildSettings.SetCdnUrlTemplate(cdnUrlTemplate);
    }

    void OnGUI()
    {
        DrawHeader();
        DrawSettings();
        DrawActions();
    }

    void DrawHeader()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("AssetBundle 打包工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("统一输出到 Build/AssetBundles/{平台}/，清单为 custom_manifest.json。构建前可自动打包 UI/View、UI/Images 图集。", MessageType.Info);
        GUILayout.Space(6);
    }

    void DrawSettings()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("输出根目录", outputPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择输出根目录", AssetBundleBuildSettings.GetFullOutputRoot(), "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (selectedPath.StartsWith(projectRoot))
                {
                    outputPath = selectedPath.Substring(projectRoot.Length).TrimStart('\\', '/');
                }
                else
                {
                    outputPath = selectedPath;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        int currentIndex = System.Array.IndexOf(AssetBundlePackager.availablePlatforms, buildTarget);
        if (currentIndex == -1)
        {
            currentIndex = 0;
        }

        currentIndex = EditorGUILayout.Popup("目标平台", currentIndex, AssetBundlePackager.platformNames);
        buildTarget = AssetBundlePackager.availablePlatforms[currentIndex];

        if (EditorGUI.EndChangeCheck())
        {
            lastVersionNumber = AssetBundleBuildSettings.ReadLastManifestVersion(outputPath, buildTarget);
            curVersionNumber = AssetBundleBuildSettings.SuggestNextManifestVersion(outputPath, buildTarget);
            cdnUrlTemplate = AssetBundleBuildSettings.BuildDefaultCdnUrlTemplate(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath)));
        }

        compressionType = (CompressionType)EditorGUILayout.EnumPopup("压缩格式", compressionType);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("上一版本 ID", lastVersionNumber.ToString());
        EditorGUI.EndDisabledGroup();
        curVersionNumber = EditorGUILayout.IntField("当前版本 ID", curVersionNumber);

        EditorGUILayout.Space(4);
        cdnUrlTemplate = EditorGUILayout.TextField("CDN URL 模板", cdnUrlTemplate);
        EditorGUILayout.HelpBox("模板中 {0} 为平台目录名。打包成功后会写入 StreamingAssets/ab_runtime_config.json。", MessageType.None);

        clearFolders = EditorGUILayout.Toggle("清空平台输出目录", clearFolders);
        copyToStreamingAssets = EditorGUILayout.Toggle("拷贝到 StreamingAssets", copyToStreamingAssets);
        packAtlasesBeforeBuild = EditorGUILayout.Toggle("构建前打包 UI 图集", packAtlasesBeforeBuild);

        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级设置");
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            if (GUILayout.Button("强制完整重建"))
            {
                if (EditorUtility.DisplayDialog("强制重建", "将重新打包所有 AssetBundles，是否继续？", "确定", "取消"))
                {
                    RunBuild(forceRebuild: true);
                }
            }

            if (GUILayout.Button("打开平台输出目录"))
            {
                string platformPath = Path.Combine(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath)),
                    AssetBundlePackager.GetPlatformFolder(buildTarget));
                if (!Directory.Exists(platformPath))
                {
                    Directory.CreateDirectory(platformPath);
                }
                EditorUtility.RevealInFinder(platformPath);
            }

            if (GUILayout.Button("仅生成/刷新运行时配置"))
            {
                AssetBundleBuildSettings.SetCdnUrlTemplate(cdnUrlTemplate);
                AssetBundleBuildSettings.WriteRuntimeConfig(buildTarget);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawActions()
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        GUI.color = Color.green;
        if (GUILayout.Button("构建 AssetBundles", GUILayout.Height(32)))
        {
            RunBuild(forceRebuild: false);
        }
        GUI.color = Color.white;

        if (GUILayout.Button("关闭", GUILayout.Height(32), GUILayout.Width(80)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    void RunBuild(bool forceRebuild)
    {
        AssetBundleBuildSettings.SetCdnUrlTemplate(cdnUrlTemplate);

        var request = new AssetBundleBuildPipeline.BuildRequest
        {
            OutputRoot = outputPath,
            BuildTarget = buildTarget,
            Compression = compressionType,
            ManifestVersion = curVersionNumber,
            ClearOutputFolder = clearFolders,
            CopyToStreamingAssets = copyToStreamingAssets,
            ForceRebuild = forceRebuild,
            ShowManifestDialog = true,
            PackAtlasesBeforeBuild = packAtlasesBeforeBuild,
        };

        AssetBundleBuildPipeline.BuildResult result = AssetBundleBuildPipeline.BuildAndManifest(request);
        if (!result.Success)
        {
            EditorUtility.DisplayDialog("打包失败", "请查看 Console 日志。", "确定");
            return;
        }

        generatedManifest = result.Manifest;
        lastVersionNumber = curVersionNumber;
        curVersionNumber = lastVersionNumber + 1;

        EditorUtility.DisplayDialog(
            "打包完成",
            "平台: " + buildTarget + "\n路径: " + result.PlatformBundlePath,
            "确定");

        CustomManifestBuilderWindow.ShowWindow(generatedManifest);
    }
}
