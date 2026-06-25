using System.IO;
using UnityEditor;
using UnityEngine;

public class CustomManifestBuilderWindow : EditorWindow
{
    private const string WndTitle = "AB 包清单";
    private const string MenuPath = "Tools/管理 AB 包清单";

    private string bundleRootPath = AssetBundleBuildSettings.DefaultOutputRoot;
    public CustomManifest generatedManifest;
    private Vector2 scrollPosition;
    private BuildTarget buildTarget = BuildTarget.StandaloneWindows64;
    private int lastVersionNumber;
    private int curVersionNumber;

    public static void ShowWindow(CustomManifest manifest)
    {
        CustomManifestBuilderWindow window = GetWindow<CustomManifestBuilderWindow>(WndTitle);
        window.minSize = new Vector2(600, 400);
        window.LoadFromSettings(manifest);
    }

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        CustomManifestBuilderWindow window = GetWindow<CustomManifestBuilderWindow>(WndTitle);
        window.minSize = new Vector2(600, 400);
        window.LoadFromSettings(null);
    }

    void LoadFromSettings(CustomManifest manifest)
    {
        bundleRootPath = AssetBundleBuildSettings.GetOutputRoot();
        buildTarget = AssetBundleBuildSettings.GetBuildTarget();
        lastVersionNumber = AssetBundleBuildSettings.ReadLastManifestVersion(bundleRootPath, buildTarget);
        curVersionNumber = AssetBundleBuildSettings.SuggestNextManifestVersion(bundleRootPath, buildTarget);
        generatedManifest = manifest;
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AssetBundle 清单管理", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("根目录下需已有平台子目录（如 StandaloneWindows）。", MessageType.Info);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        bundleRootPath = EditorGUILayout.TextField("输出根目录", bundleRootPath);

        if (GUILayout.Button("选择输出根目录"))
        {
            string picked = EditorUtility.OpenFolderPanel("选择 AB 输出根目录", AssetBundleBuildSettings.GetFullOutputRoot(), "");
            if (!string.IsNullOrEmpty(picked))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                bundleRootPath = picked.StartsWith(projectRoot)
                    ? picked.Substring(projectRoot.Length).TrimStart('\\', '/')
                    : picked;
            }
        }

        int currentIndex = System.Array.IndexOf(AssetBundlePackager.availablePlatforms, buildTarget);
        if (currentIndex == -1)
        {
            currentIndex = 0;
        }

        currentIndex = EditorGUILayout.Popup("目标平台", currentIndex, AssetBundlePackager.platformNames);
        buildTarget = AssetBundlePackager.availablePlatforms[currentIndex];

        if (EditorGUI.EndChangeCheck())
        {
            lastVersionNumber = AssetBundleBuildSettings.ReadLastManifestVersion(bundleRootPath, buildTarget);
            curVersionNumber = AssetBundleBuildSettings.SuggestNextManifestVersion(bundleRootPath, buildTarget);
        }

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("上一版本 ID", lastVersionNumber.ToString());
        EditorGUI.EndDisabledGroup();

        curVersionNumber = EditorGUILayout.IntField("当前版本 ID", curVersionNumber);

        EditorGUILayout.Space();
        if (GUILayout.Button("生成清单文件", GUILayout.Height(30)))
        {
            string fullRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", bundleRootPath));
            generatedManifest = AssetBundlePackager.GenerateManifest(
                fullRoot,
                buildTarget,
                curVersionNumber,
                (int)AssetBundleBuildSettings.GetCompressionType(),
                showSuccessDialog: true);

            if (generatedManifest != null)
            {
                AssetBundleBuildSettings.SetOutputRoot(bundleRootPath);
                AssetBundleBuildSettings.WriteRuntimeConfig(buildTarget);
                lastVersionNumber = curVersionNumber;
                curVersionNumber = lastVersionNumber + 1;
            }
        }

        if (GUILayout.Button("在资源管理器中打开平台目录", GUILayout.Height(28)))
        {
            string platformPath = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", bundleRootPath)),
                AssetBundlePackager.GetPlatformFolder(buildTarget));
            if (!Directory.Exists(platformPath))
            {
                Directory.CreateDirectory(platformPath);
            }
            EditorUtility.RevealInFinder(platformPath);
        }

        if (generatedManifest != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("当前清单信息", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("应用版本: " + generatedManifest.AppVersion);
            EditorGUILayout.LabelField("清单版本: " + generatedManifest.ManifestVersion);
            EditorGUILayout.LabelField("构建时间: " + generatedManifest.BuildTime);
            EditorGUILayout.LabelField("AB 包数量: " + generatedManifest.AssetBundles.Count);
            EditorGUILayout.LabelField("压缩方式: " + generatedManifest.CompressedFormat);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AB 包列表:");

            foreach (CustomAssetBundleInfo bundle in generatedManifest.AssetBundles)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("资源: " + bundle.AssetName);
                EditorGUILayout.LabelField("路径: " + bundle.BundleName);
                EditorGUILayout.LabelField("大小: " + bundle.Size + " bytes");
                EditorGUILayout.LabelField("哈希: " + bundle.Hash);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
