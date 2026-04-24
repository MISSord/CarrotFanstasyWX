using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public class AtlasPacker : EditorWindow
{
    private string targetFolderPath = "Assets";
    private bool includeSubdirectories = true;
    private string atlasNameSuffix = "_Atlas";
    private int maxAtlasSize = 2048;
    private bool generateForEachSubfolder = false;

    [MenuItem("Tools/图集打包工具")]
    public static void ShowWindow()
    {
        GetWindow<AtlasPacker>("图集打包工具");
    }

    private void OnGUI()
    {
        GUILayout.Label("图集打包设置", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // 目标文件夹选择
        EditorGUILayout.BeginHorizontal();
        targetFolderPath = EditorGUILayout.TextField("目标文件夹", targetFolderPath);
        if (GUILayout.Button("选择", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("选择目标文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                targetFolderPath = "Assets" + path.Replace(Application.dataPath, "");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 打包选项
        includeSubdirectories = EditorGUILayout.Toggle("包含子文件夹", includeSubdirectories);
        generateForEachSubfolder = EditorGUILayout.Toggle("为每个子文件夹生成图集", generateForEachSubfolder);
        atlasNameSuffix = EditorGUILayout.TextField("图集名称后缀", atlasNameSuffix);
        maxAtlasSize = EditorGUILayout.IntField("最大图集尺寸", maxAtlasSize);

        EditorGUILayout.Space();

        // 功能按钮
        if (GUILayout.Button("打包指定文件夹图集", GUILayout.Height(30)))
        {
            PackAtlasForTargetFolder();
        }

        if (GUILayout.Button("为每个子文件夹生成独立图集", GUILayout.Height(30)))
        {
            PackAtlasForEachSubfolder();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("功能1：在目标文件夹中查找所有包含Image文件夹的子文件夹并打包图集\n功能2：为每个子文件夹生成独立的图集", MessageType.Info);
    }

    /// <summary>
    /// 功能1：遍历目标文件夹，为包含Image文件夹的子文件夹打包图集
    /// </summary>
    private void PackAtlasForTargetFolder()
    {
        if (!Directory.Exists(targetFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "目标文件夹不存在！", "确定");
            return;
        }

        int processedCount = 0;
        int atlasCreatedCount = 0;

        // 获取所有子文件夹
        string[] allSubfolders = Directory.GetDirectories(targetFolderPath, "*", includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        foreach (string folderPath in allSubfolders)
        {
            string relativeFolderPath = folderPath.Replace("\\", "/");

            // 检查是否包含Image文件夹
            string imageFolderPath = Path.Combine(folderPath, "Images").Replace("\\", "/");
            if (Directory.Exists(imageFolderPath))
            {
                processedCount++;
                if (PackImagesInFolder(imageFolderPath, GetAtlasNameFromPath(folderPath)))
                {
                    atlasCreatedCount++;
                }
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"处理完成！\n检查了 {processedCount} 个文件夹\n创建了 {atlasCreatedCount} 个图集", "确定");
    }

    /// <summary>
    /// 功能2：为每个子文件夹生成独立图集
    /// </summary>
    private void PackAtlasForEachSubfolder()
    {
        if (!Directory.Exists(targetFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "目标文件夹不存在！", "确定");
            return;
        }

        int processedCount = 0;
        int atlasCreatedCount = 0;

        // 只获取第一层子文件夹
        string[] firstLevelSubfolders = Directory.GetDirectories(targetFolderPath, "*", SearchOption.TopDirectoryOnly);

        foreach (string folderPath in firstLevelSubfolders)
        {
            processedCount++;
            if (PackImagesInFolder(folderPath, GetAtlasNameFromPath(folderPath)))
            {
                atlasCreatedCount++;
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"处理完成！\n处理了 {processedCount} 个子文件夹\n创建了 {atlasCreatedCount} 个图集", "确定");
    }

    /// <summary>
    /// 打包指定文件夹中的所有图片为图集
    /// </summary>
    private bool PackImagesInFolder(string folderPath, string atlasName)
    {
        string relativeFolderPath = folderPath.Replace("\\", "/");
        if (!relativeFolderPath.StartsWith("Assets/"))
        {
            relativeFolderPath = "Assets" + relativeFolderPath.Substring(Application.dataPath.Length);
        }

        // 获取文件夹中的所有图片
        string[] imagePaths = Directory.GetFiles(relativeFolderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => IsImageFile(path))
            .ToArray();

        if (imagePaths.Length == 0)
        {
            Debug.LogWarning($"文件夹 {relativeFolderPath} 中没有找到图片文件");
            return false;
        }

        Debug.Log($"在文件夹 {relativeFolderPath} 中找到 {imagePaths.Length} 张图片");

        // 创建SpriteAtlas资产
        string atlasPath = Path.Combine(relativeFolderPath, atlasName + ".spriteatlas").Replace("\\", "/");
        SpriteAtlas atlas = new SpriteAtlas();

        // 设置图集参数
        SpriteAtlasPackingSettings packingSettings = new SpriteAtlasPackingSettings
        {
            padding = 4,
            enableRotation = false,
            enableTightPacking = true
        };
        atlas.SetPackingSettings(packingSettings);

        SpriteAtlasTextureSettings textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false,
            generateMipMaps = false,
            filterMode = FilterMode.Bilinear
        };
        atlas.SetTextureSettings(textureSettings);

        // 收集所有Sprite
        List<Sprite> sprites = new List<Sprite>();
        List<Texture2D> textures = new List<Texture2D>();

        foreach (string imagePath in imagePaths)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(imagePath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
                else if (asset is Texture2D texture)
                {
                    textures.Add(texture);
                }
            }
        }

        // 添加对象到图集
        if (sprites.Count > 0)
        {
            atlas.Add(sprites.ToArray());
        }
        if (textures.Count > 0)
        {
            atlas.Add(textures.ToArray());
        }

        // 保存图集
        AssetDatabase.CreateAsset(atlas, atlasPath);
        Debug.Log($"创建图集: {atlasPath}");

        return true;
    }

    /// <summary>
    /// 从文件夹路径生成图集名称
    /// </summary>
    private string GetAtlasNameFromPath(string folderPath)
    {
        string folderName = Path.GetFileName(folderPath);
        return folderName + atlasNameSuffix;
    }

    /// <summary>
    /// 检查文件是否为图片文件
    /// </summary>
    private bool IsImageFile(string path)
    {
        string extension = Path.GetExtension(path).ToLower();
        return extension == ".png" || extension == ".jpg" ;
    }
}