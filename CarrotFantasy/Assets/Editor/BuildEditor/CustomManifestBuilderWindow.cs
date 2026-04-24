using System.IO;
using UnityEditor;
using UnityEngine;

public class CustomManifestBuilderWindow : EditorWindow
{
    private const string WndTitle = "AB \u5305\u6e05\u5355";
    private const string MenuPath = "Tools/\u7ba1\u7406 AB \u5305\u6e05\u5355";

    private string bundlePath = "Assets/StreamingAssets/AssetBundles";
    public CustomManifest generatedManifest;
    private Vector2 scrollPosition;
    private BuildTarget buildTarget = BuildTarget.StandaloneWindows;
    private int LastVersionNumber = 0;
    private int CurVersionNumber = 0;

    public static void ShowWindow(CustomManifest Manifest)
    {
        var window = GetWindow<CustomManifestBuilderWindow>(WndTitle);
        window.minSize = new Vector2(600, 400);
        window.InitData(Manifest);
    }

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        var window = GetWindow<CustomManifestBuilderWindow>(WndTitle);
        window.minSize = new Vector2(600, 400);
        window.InitData(null);
    }

    private void InitData(CustomManifest Manifest)
    {
        string localPath = UnityEditor.EditorPrefs.GetString("OutPutPath");
        if (localPath != "")
        {
            bundlePath = localPath;
        }

        int target = UnityEditor.EditorPrefs.GetInt("BuildTarget", -1);
        if (target == -1) target = (int)EditorUserBuildSettings.activeBuildTarget;

        string path = localPath + "/" + AssetBundlePackager.GetPlatformFolder((BuildTarget)target) + "/custom_manifest.json";
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            CustomManifest old = JsonUtility.FromJson<CustomManifest>(text);
            LastVersionNumber = old.ManifestVersion;
        }
        else
        {
            LastVersionNumber = 0;
        }

        CurVersionNumber = LastVersionNumber;
        buildTarget = (UnityEditor.BuildTarget)target;
        generatedManifest = Manifest;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AssetBundle \u6e05\u5355\u7ba1\u7406", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        bundlePath = EditorGUILayout.TextField("AB \u5305\u8def\u5f84", bundlePath);

        if (GUILayout.Button("\u9009\u62e9 AB \u5305\u76ee\u5f55"))
        {
            bundlePath = EditorUtility.OpenFolderPanel("\u9009\u62e9 AB \u5305\u76ee\u5f55", bundlePath, "");
            Repaint();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("\u5f53\u524d\u76ee\u5f55: " + bundlePath, MessageType.Info);

        int currentIndex = System.Array.IndexOf(AssetBundlePackager.availablePlatforms, buildTarget);
        if (currentIndex == -1) currentIndex = 0;

        currentIndex = EditorGUILayout.Popup("\u76ee\u6807\u5e73\u53f0", currentIndex, AssetBundlePackager.platformNames);
        buildTarget = AssetBundlePackager.availablePlatforms[currentIndex];

        if (EditorGUI.EndChangeCheck())
        {
            string path = bundlePath + "/" + AssetBundlePackager.GetPlatformFolder(buildTarget) + "/custom_manifest.json";
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                CustomManifest old = JsonUtility.FromJson<CustomManifest>(text);
                LastVersionNumber = old.ManifestVersion;
            }
            else
            {
                LastVersionNumber = 0;
            }
        }

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("\u4e0a\u4e00\u7248\u672c ID", LastVersionNumber.ToString());
        EditorGUI.EndDisabledGroup();

        CurVersionNumber = EditorGUILayout.IntField("\u5f53\u524d\u7248\u672c ID", CurVersionNumber);

        EditorGUILayout.Space();
        if (GUILayout.Button("\u751f\u6210\u6e05\u5355\u6587\u4ef6", GUILayout.Height(30)))
        {
            generatedManifest = AssetBundlePackager.GenerateManifest(bundlePath, buildTarget, CurVersionNumber);
        }

        if (GUILayout.Button("\u5728\u8d44\u6e90\u7ba1\u7406\u5668\u4e2d\u6253\u5f00", GUILayout.Height(30)))
        {
            if (Directory.Exists(bundlePath))
            {
                EditorUtility.RevealInFinder(bundlePath);
            }
        }

        if (generatedManifest != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("\u5f53\u524d\u6e05\u5355\u4fe1\u606f", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("\u5e94\u7528\u7248\u672c: " + generatedManifest.AppVersion);
            EditorGUILayout.LabelField("\u6e05\u5355\u7248\u672c: " + generatedManifest.ManifestVersion);
            EditorGUILayout.LabelField("\u6784\u5efa\u65f6\u95f4: " + generatedManifest.BuildTime);
            EditorGUILayout.LabelField("AB \u5305\u6570\u91cf: " + generatedManifest.AssetBundles.Count);
            EditorGUILayout.LabelField("\u538b\u7f29\u65b9\u5f0f: " + generatedManifest.CompressedFormat);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AB \u5305\u5217\u8868:");

            foreach (var bundle in generatedManifest.AssetBundles)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("\u8d44\u6e90: " + bundle.AssetName);
                EditorGUILayout.LabelField("\u8def\u5f84: " + bundle.BundleName);
                EditorGUILayout.LabelField("\u7248\u672c: " + bundle.Version);
                EditorGUILayout.LabelField("\u5927\u5c0f: " + bundle.Size + " bytes");
                EditorGUILayout.LabelField("\u54c8\u5e0c: " + bundle.Hash);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
