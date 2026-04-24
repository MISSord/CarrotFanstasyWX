using Codice.CM.Client.Differences;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class ABPackagerWindow : EditorWindow
{
    // 打包参数
    private string outputPath = "AssetBundles";
    private BuildTarget buildTarget = BuildTarget.StandaloneWindows;
    private CompressionType compressionType = CompressionType.ChunkBasedCompression;
    private bool clearFolders = true;
    private bool copyToStreamingAssets = false;
    private int LastVersionNumber = 0;
    private int CurVersionNumber = 0;

    // 界面状态
    private Vector2 scrollPosition;
    private bool showAdvancedSettings = false;

    private CustomManifest generatedManifest;

    private void OnDestroy()
    {
        UnityEditor.EditorPrefs.SetString("OutPutPath", outputPath);
        UnityEditor.EditorPrefs.SetInt("BuildTarget", (int)buildTarget);
        UnityEditor.EditorPrefs.SetInt("CompressionType", (int)compressionType);
    }


    [MenuItem("Tools/打开AssetBundle打包窗口")]
    public static void ShowWindow()
    {
        // 创建窗口
        ABPackagerWindow window = GetWindow<ABPackagerWindow>("AB Packager");
        window.minSize = new Vector2(400, 500);
        window.InitLocalData();
        window.Show();
    }

    private void InitLocalData()
    {
        string localPath = UnityEditor.EditorPrefs.GetString("OutPutPath");
        if (localPath == "") localPath = "AssetBundles";

        int target = UnityEditor.EditorPrefs.GetInt("BuildTarget", -1);
        if (target == -1) target = (int)EditorUserBuildSettings.activeBuildTarget;

        int type = UnityEditor.EditorPrefs.GetInt("CompressionType",-1);
        if (type == -1) type = (int)CompressionType.ChunkBasedCompression;

        //获取之前打包的结果
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

        outputPath = localPath;
        buildTarget = (UnityEditor.BuildTarget)target;
        compressionType = (CompressionType)type;
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawSettings();
        DrawActions();
        DrawLog();
    }

    private void DrawHeader()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("AssetBundle 打包工具", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("快速配置和打包AssetBundles", EditorStyles.helpBox);
        GUILayout.Space(10);
    }

    private void DrawSettings()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 输出路径
        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("输出路径", outputPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.SaveFolderPanel("选择输出路径", "", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                outputPath = selectedPath;
            }
        }
        EditorGUILayout.EndHorizontal();

        // 平台选择
        int currentIndex = System.Array.IndexOf(AssetBundlePackager.availablePlatforms, buildTarget);
        if (currentIndex == -1) currentIndex = 0;

        currentIndex = EditorGUILayout.Popup("目标平台", currentIndex, AssetBundlePackager.platformNames);
        buildTarget = AssetBundlePackager.availablePlatforms[currentIndex];

        if (EditorGUI.EndChangeCheck())
        {
            //获取之前打包的结果
            string path = outputPath + "/" + AssetBundlePackager.GetPlatformFolder(buildTarget) + "/custom_manifest.json";
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

        //压缩格式
        compressionType = (CompressionType)EditorGUILayout.EnumPopup("压缩格式", compressionType);

        // 版本编号
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("上一个版本ID", LastVersionNumber.ToString());
        EditorGUI.EndDisabledGroup();
        CurVersionNumber = EditorGUILayout.IntField("当前版本ID", CurVersionNumber);

        // 显示压缩格式说明
        string compressionInfo = GetCompressionInfo();
        EditorGUILayout.HelpBox(compressionInfo, MessageType.Info);

        // 基础选项
        clearFolders = EditorGUILayout.Toggle("清空输出文件夹", clearFolders);
        copyToStreamingAssets = EditorGUILayout.Toggle("拷贝到StreamingAssets", copyToStreamingAssets);

        // 高级设置
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级设置");
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            DrawAdvancedSettings();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndScrollView();
    }

    private string GetCompressionInfo()
    {
        switch (compressionType)
        {
            case CompressionType.StandardCompression:
                return "LZMA压缩: 高压缩率，但需要整体解压。适合整体加载的资源。";
            case CompressionType.ChunkBasedCompression:
                return "LZ4压缩: 较好的压缩率，可以按需解压。推荐使用。";
            case CompressionType.NoCompression:
                return "无压缩: 快速加载，但文件体积较大。";
            default:
                return "";
        }
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.LabelField("强制重新打包所有AssetBundles", EditorStyles.miniLabel);
        if (GUILayout.Button("强制完整重建"))
        {
            if (EditorUtility.DisplayDialog("强制重建", "这将重新打包所有AssetBundles，确定要继续吗？", "确定", "取消"))
            {
                bool isSuccess = BuildBundles(true);
                if (isSuccess) 
                    generatedManifest = AssetBundlePackager.GenerateManifest(outputPath, buildTarget, CurVersionNumber, (int)compressionType);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("工具", EditorStyles.miniLabel);
        if (GUILayout.Button("打开输出文件夹"))
        {
            OpenOutputFolder();
        }

        //if (GUILayout.Button("清空所有AssetBundle名称"))
        //{
        //    ClearAllAssetBundleNames();
        //}
    }

    private void DrawActions()
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        // 构建按钮
        GUI.color = Color.green;
        if (GUILayout.Button("构建 AssetBundles", GUILayout.Height(30)))
        {
            bool isSuccess = BuildBundles(false);
            if (isSuccess)
            {
                generatedManifest = AssetBundlePackager.GenerateManifest(outputPath, buildTarget, CurVersionNumber, (int)compressionType);
                CustomManifestBuilderWindow.ShowWindow(generatedManifest);
            }

        }
        GUI.color = Color.white;

        // 取消按钮
        if (GUILayout.Button("取消", GUILayout.Height(30)))
        {
            this.Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLog()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("操作日志", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("构建完成后将显示详细日志信息。", MessageType.Info);
    }

    private bool BuildBundles(bool isForce)
    {
        try
        {
            // 获取压缩选项
            BuildAssetBundleOptions options = BuildAssetBundleOptions.ForceRebuildAssetBundle;
            if (isForce != true) options = AssetBundlePackager.GetCompressionOption(compressionType);

            // 添加平台子路径
            string platformPath = outputPath + "/" + AssetBundlePackager.GetPlatformFolder(buildTarget);

            // 执行打包
            bool success = AssetBundlePackager.BuildAssetBundles(
                platformPath,
                buildTarget,
                options,
                clearFolders,
                copyToStreamingAssets
            );

            if (success)
            {
                Debug.Log($"✅ AssetBundle打包成功！平台: {buildTarget} 路径: {platformPath}");
                EditorUtility.DisplayDialog("打包完成", $"AssetBundle打包成功！\n平台: {buildTarget}\n路径: {platformPath}", "确定");
                return true;
            }
            else
            {
                Debug.LogError($"❌ AssetBundle打包失败！");
                EditorUtility.DisplayDialog("打包失败", "AssetBundle打包失败，请查看控制台日志获取详细信息。", "确定");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"打包过程发生错误: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("错误", $"打包过程发生错误: {e.Message}", "确定");
            return false;
        }
    }

    private void OpenOutputFolder()
    {
        string fullPath = Path.GetFullPath(outputPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
        EditorUtility.RevealInFinder(fullPath);
    }

    //private void ClearAllAssetBundleNames()
    //{
    //    if (EditorUtility.DisplayDialog("清空AssetBundle名称", "这将清除项目中所有资源的AssetBundle名称设置，确定要继续吗？", "确定", "取消"))
    //    {
    //        string[] allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
    //        foreach (string assetBundleName in allAssetBundleNames)
    //        {
    //            AssetDatabase.RemoveAssetBundleName(assetBundleName, true);
    //        }
    //        Debug.Log("已清空所有AssetBundle名称");
    //    }
    //}
}