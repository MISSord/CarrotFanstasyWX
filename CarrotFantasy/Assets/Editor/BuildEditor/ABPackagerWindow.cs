using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AB 打包工具窗口。实际构建走 AssetBundleBuildPipeline.BuildAndManifest：
/// 图集 → Build AB → 生成清单+Pack → 可选 StreamingAssets / 云上传。
/// </summary>
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
    private bool showDeploySettings;
    private CustomManifest generatedManifest;

    private string deployHost;
    private int deployPort;
    private string deployUser;
    private string deployPassword;
    private string deployRemotePath;
    private bool deployUsePrivateKey;
    private string deployPrivateKeyPath;
    private bool promptUploadAfterBuild;

    [MenuItem("Tools/打开AssetBundle打包窗口")]
    public static void ShowWindow()
    {
        ABPackagerWindow window = GetWindow<ABPackagerWindow>("AB Packager");
        window.minSize = new Vector2(420, 640);
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
        LoadDeploySettings();
    }

    void LoadDeploySettings()
    {
        deployHost = AssetBundleDeploySettings.Host;
        deployPort = AssetBundleDeploySettings.Port;
        deployUser = AssetBundleDeploySettings.User;
        deployPassword = AssetBundleDeploySettings.Password;
        deployRemotePath = AssetBundleDeploySettings.RemotePath;
        deployUsePrivateKey = AssetBundleDeploySettings.UsePrivateKey;
        deployPrivateKeyPath = AssetBundleDeploySettings.PrivateKeyPath;
        promptUploadAfterBuild = AssetBundleDeploySettings.PromptUploadAfterBuild;
    }

    void SaveDeploySettings()
    {
        AssetBundleDeploySettings.Host = deployHost;
        AssetBundleDeploySettings.Port = deployPort;
        AssetBundleDeploySettings.User = deployUser;
        AssetBundleDeploySettings.Password = deployPassword;
        AssetBundleDeploySettings.RemotePath = deployRemotePath;
        AssetBundleDeploySettings.UsePrivateKey = deployUsePrivateKey;
        AssetBundleDeploySettings.PrivateKeyPath = deployPrivateKeyPath;
        AssetBundleDeploySettings.PromptUploadAfterBuild = promptUploadAfterBuild;
    }

    void OnDestroy()
    {
        AssetBundleBuildSettings.SetOutputRoot(outputPath);
        AssetBundleBuildSettings.SetBuildTarget(buildTarget);
        AssetBundleBuildSettings.SetCompressionType(compressionType);
        AssetBundleBuildSettings.SetCdnUrlTemplate(cdnUrlTemplate);
        SaveDeploySettings();
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
        EditorGUILayout.HelpBox(GetCompressionFormatHint(compressionType), MessageType.None);

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

        DrawDeploySettings();

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

            if (GUILayout.Button("测试上传当前平台目录"))
            {
                SaveDeploySettings();
                TryUploadPlatformBundle(GetCurrentPlatformBundlePath());
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawDeploySettings()
    {
        showDeploySettings = EditorGUILayout.Foldout(showDeploySettings, "云服务器上传 (SFTP)");
        if (!showDeploySettings)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.HelpBox(
            "打包成功后可一键上传到 Nginx 静态目录。\n"
            + "主机 / 账号 / 密码等保存在本机 EditorPrefs，不会写入工程，也不会提交到 Git。",
            MessageType.Info);

        deployHost = EditorGUILayout.TextField("服务器地址", deployHost);
        deployPort = EditorGUILayout.IntField("SSH 端口", deployPort);
        deployUser = EditorGUILayout.TextField("用户名", deployUser);
        deployRemotePath = EditorGUILayout.TextField("远程目录", deployRemotePath);
        promptUploadAfterBuild = EditorGUILayout.Toggle("打包完成后询问上传", promptUploadAfterBuild);

        deployUsePrivateKey = EditorGUILayout.Toggle("使用私钥登录", deployUsePrivateKey);
        if (deployUsePrivateKey)
        {
            EditorGUILayout.BeginHorizontal();
            deployPrivateKeyPath = EditorGUILayout.TextField("私钥路径", deployPrivateKeyPath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFilePanel("选择 SSH 私钥", string.Empty, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    deployPrivateKeyPath = picked;
                }
            }
            EditorGUILayout.EndHorizontal();
            deployPassword = EditorGUILayout.PasswordField("私钥口令(可选)", deployPassword);
        }
        else
        {
            deployPassword = EditorGUILayout.PasswordField("SSH 密码", deployPassword);
        }

        EditorGUILayout.HelpBox("远程目录示例: /var/www/your-game/ab/StandaloneWindows", MessageType.None);
        EditorGUI.indentLevel--;
    }

    string GetCurrentPlatformBundlePath()
    {
        return Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath)),
            AssetBundlePackager.GetPlatformFolder(buildTarget));
    }

    /// <summary>压缩格式与实际产物算法的对应说明（写入清单 CompressedFormat）。</summary>
    static string GetCompressionFormatHint(CompressionType compression)
    {
        switch (compression)
        {
            case CompressionType.StandardCompression:
                return "StandardCompression → 产物为 LZMA（CompressedFormat=0）。体积小，下载后需再压成 LZ4 才能高效加载。";
            case CompressionType.ChunkBasedCompression:
                return "ChunkBasedCompression → 产物为 LZ4（CompressedFormat=1）。推荐：体积与加载速度较均衡，下载后无需再转换。";
            case CompressionType.NoCompression:
                return "NoCompression → 产物无压缩（CompressedFormat=2）。体积最大，加载最快，适合本地调试。";
            default:
                return "未知压缩格式。";
        }
    }

    void DrawActions()
    {
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.45f, 0.75f, 1f);
        if (GUILayout.Button("仅热更代码（Generate + 同步 DLL + 清单）", GUILayout.Height(32)))
        {
            SaveDeploySettings();
            if (buildTarget != EditorUserBuildSettings.activeBuildTarget)
            {
                EditorUtility.DisplayDialog(
                    "平台不一致",
                    "窗口目标平台与 Build Settings 激活平台不一致。\n请先切换 Build Settings 到: "
                    + buildTarget,
                    "确定");
            }
            else
            {
                HybridCLRCodeHotUpdatePipeline.Run(buildTarget, promptUpload: true);
                lastVersionNumber = AssetBundleBuildSettings.ReadLastManifestVersion(outputPath, buildTarget);
                curVersionNumber = AssetBundleBuildSettings.SuggestNextManifestVersion(outputPath, buildTarget);
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "「仅热更代码」不重打 Unity AB：执行 HybridCLR Generate/All，同步 DLL，刷新清单与 Packs，完成后询问是否上传（仅 hybridclr/packs/清单）。",
            MessageType.None);

        GUILayout.Space(6);
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

        // forceRebuild 时 Pipeline 会 OR ForceRebuildAssetBundle，并保留所选压缩格式。
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
        SaveDeploySettings();

        if (promptUploadAfterBuild
            && EditorUtility.DisplayDialog(
                "上传云端",
                "AssetBundle 已打包完成。\n是否立即上传到云服务器？\n\n"
                    + deployHost + ":" + deployRemotePath,
                "上传",
                "稍后"))
        {
            TryUploadPlatformBundle(result.PlatformBundlePath);
        }
    }

    void TryUploadPlatformBundle(string platformBundlePath)
    {
        if (string.IsNullOrEmpty(platformBundlePath) || !Directory.Exists(platformBundlePath))
        {
            EditorUtility.DisplayDialog("上传失败", "平台输出目录不存在:\n" + platformBundlePath, "确定");
            return;
        }

        if (!AssetBundleDeploySettings.TryValidate(out string validationError))
        {
            EditorUtility.DisplayDialog("上传失败", validationError, "确定");
            return;
        }

        AssetBundleCloudUploadResult uploadResult = AssetBundleCloudUploader.UploadDirectory(platformBundlePath);
        if (uploadResult.Cancelled)
        {
            EditorUtility.DisplayDialog(
                "上传已取消",
                string.Format("已上传 {0} 个文件。", uploadResult.UploadedFileCount),
                "确定");
            return;
        }

        if (!uploadResult.Success)
        {
            EditorUtility.DisplayDialog("上传失败", uploadResult.Message, "确定");
            return;
        }

        EditorUtility.DisplayDialog(
            "上传完成",
            uploadResult.Message
                + "\n\n服务器: " + deployHost
                + "\n目录: " + deployRemotePath
                + "\n\n可访问:\n"
                + BuildManifestVerifyUrl(),
            "确定");
    }

    string BuildManifestVerifyUrl()
    {
        string template = string.IsNullOrEmpty(cdnUrlTemplate)
            ? AssetBundleBuildSettings.GetCdnUrlTemplate()
            : cdnUrlTemplate;
        if (string.IsNullOrEmpty(template))
        {
            return "(请先在 CDN URL 模板中配置 http 地址)";
        }

        string platformFolder = AssetBundlePackager.GetPlatformFolder(buildTarget);
        return template.Replace("{0}", platformFolder) + "/custom_manifest.json";
    }
}
