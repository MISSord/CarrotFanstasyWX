using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <b>只读</b>的清单查看器：显示「当前版本」的 <c>custom_manifest.json</c>（上次打包产物）。
/// <para>
/// 生成清单的能力已经移到 Profile 的打包步骤（GenerateManifestData / MergePacks /
/// WriteManifestJson / SignManifest / WriteVersion），这个窗口只负责**显示**：
/// 不再提供输出目录、平台、版本号编辑与「生成清单」按钮。
/// </para>
/// </summary>
public sealed class CustomManifestBuilderWindow : EditorWindow
{
    private const string WndTitle = "AB 清单";
    private const string MenuPath = "Tools/AssetBundle/查看清单";

    private BuildTarget viewTarget;
    private CustomManifest manifest;
    private string manifestPath;
    private string loadError;
    private Vector2 scroll;
    private bool showBundles = true;
    private bool showPacks;

    [MenuItem(MenuPath, false, 301)]
    public static void ShowWindow()
    {
        CustomManifestBuilderWindow window = GetWindow<CustomManifestBuilderWindow>(WndTitle);
        window.minSize = new Vector2(640f, 480f);
        window.LoadFromDisk();
    }

    private void OnEnable()
    {
        // 打开窗口就有内容；按钮用于"重新读取"。
        if (this.manifest == null)
        {
            this.LoadFromDisk();
        }
    }

    /// <summary>按「上次打包写入的设置」定位并读取清单。</summary>
    private void LoadFromDisk()
    {
        this.manifest = null;
        this.loadError = null;

        if (System.Array.IndexOf(AssetBundlePackager.availablePlatforms, this.viewTarget) < 0)
        {
            this.viewTarget = AssetBundleBuildSettings.GetBuildTarget();
            if (System.Array.IndexOf(AssetBundlePackager.availablePlatforms, this.viewTarget) < 0)
            {
                this.viewTarget = AssetBundlePackager.availablePlatforms[0];
            }
        }

        string outputRoot = AssetBundleBuildSettings.GetOutputRoot();
        string platformFolder = AssetBundlePackager.GetPlatformFolder(this.viewTarget);
        string platformPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", outputRoot, platformFolder));
        this.manifestPath = Path.Combine(platformPath, "custom_manifest.json");

        if (!File.Exists(this.manifestPath))
        {
            this.loadError = "还没找到清单文件：\n" + this.manifestPath
                             + "\n\n请先在 AbBuildProfile 里点「开始打包」生成一次。";
            return;
        }

        try
        {
            string json = File.ReadAllText(this.manifestPath);
            this.manifest = JsonUtility.FromJson<CustomManifest>(json);
            if (this.manifest == null)
            {
                this.loadError = "清单解析结果为空，文件可能损坏：" + this.manifestPath;
            }
        }
        catch (Exception e)
        {
            this.loadError = "读取清单失败：" + e.Message + "\n" + this.manifestPath;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AssetBundle 清单（只读）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "这里显示上次打包生成的清单；生成/修改请用 AbBuildProfile 的「开始打包」。",
            MessageType.Info);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            int index = Mathf.Max(0, System.Array.IndexOf(AssetBundlePackager.availablePlatforms, this.viewTarget));
            int newIndex = EditorGUILayout.Popup("查看平台", index, AssetBundlePackager.platformNames);
            if (newIndex != index)
            {
                this.viewTarget = AssetBundlePackager.availablePlatforms[newIndex];
                this.LoadFromDisk();
            }

            if (GUILayout.Button("显示当前版本的清单", GUILayout.Width(180f), GUILayout.Height(22f)))
            {
                this.LoadFromDisk();
            }
        }

        using (new EditorGUI.DisabledGroupScope(true))
        {
            EditorGUILayout.TextField("清单路径", this.manifestPath ?? string.Empty);
        }

        if (!string.IsNullOrEmpty(this.loadError))
        {
            EditorGUILayout.HelpBox(this.loadError, MessageType.Warning);
            return;
        }

        if (this.manifest == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("概要", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("应用版本", this.manifest.AppVersion);
        EditorGUILayout.LabelField("清单版本", this.manifest.ManifestVersion.ToString());
        EditorGUILayout.LabelField("构建时间", this.manifest.BuildTime);
        EditorGUILayout.LabelField("压缩方式", DescribeCompression(this.manifest.CompressedFormat));
        EditorGUILayout.LabelField("AB 包数量", this.manifest.AssetBundles != null ? this.manifest.AssetBundles.Count.ToString() : "0");
        EditorGUILayout.LabelField(
            "打包数量",
            this.manifest.DownloadPacks != null ? this.manifest.DownloadPacks.Count.ToString() : "0");
        EditorGUILayout.LabelField("签名文件", File.Exists(this.manifestPath + ".sig") ? "有 (.sig)" : "无（开发包）");

        this.scroll = EditorGUILayout.BeginScrollView(this.scroll);

        this.showBundles = EditorGUILayout.Foldout(
            this.showBundles,
            "AB 包列表（" + (this.manifest.AssetBundles != null ? this.manifest.AssetBundles.Count : 0) + "）",
            true);
        if (this.showBundles && this.manifest.AssetBundles != null)
        {
            for (int i = 0; i < this.manifest.AssetBundles.Count; i++)
            {
                CustomAssetBundleInfo bundle = this.manifest.AssetBundles[i];
                if (bundle == null)
                {
                    continue;
                }

                int depCount = bundle.Dependencies != null ? bundle.Dependencies.Length : 0;
                EditorGUILayout.LabelField(string.Format(
                    "{0}   {1:N1} KB   依赖 {2}",
                    bundle.BundleName,
                    bundle.Size / 1024f,
                    depCount));
            }
        }

        EditorGUILayout.Space();
        this.showPacks = EditorGUILayout.Foldout(
            this.showPacks,
            "下载 Pack（" + (this.manifest.DownloadPacks != null ? this.manifest.DownloadPacks.Count : 0) + "）",
            true);
        if (this.showPacks && this.manifest.DownloadPacks != null)
        {
            for (int i = 0; i < this.manifest.DownloadPacks.Count; i++)
            {
                DownloadPackInfo pack = this.manifest.DownloadPacks[i];
                if (pack == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField(string.Format(
                    "{0}   {1:N1} MB   {2} 个 AB",
                    pack.PackFileName,
                    pack.PackSize / 1024f / 1024f,
                    pack.BundleNames != null ? pack.BundleNames.Length : 0));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static string DescribeCompression(int compressedFormat)
    {
        switch (compressedFormat)
        {
            case 0:
                return "0（LZMA，运行时需转换为 LZ4）";
            case 1:
                return "1（LZ4 / ChunkBased）";
            case 2:
                return "2（不压缩）";
            default:
                return compressedFormat.ToString();
        }
    }
}
