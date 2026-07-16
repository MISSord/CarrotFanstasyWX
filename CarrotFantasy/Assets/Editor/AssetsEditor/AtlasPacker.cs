using UnityEditor;
using UnityEngine;

public class AtlasPacker : EditorWindow
{
    private string targetFolderPath = "Assets";
    private bool includeSubdirectories = true;

    [MenuItem("Tools/AssetBundle/图集打包", false, 302)]
    public static void ShowWindow()
    {
        GetWindow<AtlasPacker>("图集打包工具");
    }

    private void OnGUI()
    {
        GUILayout.Label("图集打包设置", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUI.color = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("默认打包（UI/View + UI/Images）", GUILayout.Height(36)))
        {
            RunDefaultPack();
        }
        GUI.color = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("自定义打包", EditorStyles.boldLabel);
        EditorGUILayout.Space();

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
        includeSubdirectories = EditorGUILayout.Toggle("包含子文件夹", includeSubdirectories);

        EditorGUILayout.Space();

        if (GUILayout.Button("打包指定文件夹图集", GUILayout.Height(30)))
        {
            RunTargetFolderPack();
        }

        if (GUILayout.Button("为每个子文件夹生成独立图集", GUILayout.Height(30)))
        {
            RunEachSubfolderPack();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "默认打包：\n" +
            "  • " + AtlasPackager.UiViewRoot + " → 查找含 Images 子文件夹并打包\n" +
            "  • " + AtlasPackager.UiImagesRoot + " → 每个第一层子文件夹独立图集\n\n" +
            "AB 打包前也会自动执行上述默认打包。",
            MessageType.Info);
    }

    private void RunDefaultPack()
    {
        AtlasPackager.DefaultPackResult result = AtlasPackager.PackDefaultUiAtlases();
        EditorUtility.DisplayDialog(
            "默认打包完成",
            string.Format(
                "UI/View：检查 {0} 个，创建/更新 {1} 个，跳过 {2} 个\n\n" +
                "UI/Images：检查 {3} 个，创建/更新 {4} 个，跳过 {5} 个",
                result.ViewResult.ProcessedCount,
                result.ViewResult.CreatedCount,
                result.ViewResult.SkippedCount,
                result.ImagesResult.ProcessedCount,
                result.ImagesResult.CreatedCount,
                result.ImagesResult.SkippedCount),
            "确定");
    }

    private void RunTargetFolderPack()
    {
        AtlasPackager.PackResult result = AtlasPackager.PackAtlasForTargetFolder(
            targetFolderPath,
            includeSubdirectories);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "完成",
            string.Format(
                "处理完成！\n检查了 {0} 个文件夹\n创建/更新了 {1} 个图集\n跳过 {2} 个",
                result.ProcessedCount,
                result.CreatedCount,
                result.SkippedCount),
            "确定");
    }

    private void RunEachSubfolderPack()
    {
        AtlasPackager.PackResult result = AtlasPackager.PackAtlasForEachSubfolder(targetFolderPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "完成",
            string.Format(
                "处理完成！\n处理了 {0} 个子文件夹\n创建/更新了 {1} 个图集\n跳过 {2} 个",
                result.ProcessedCount,
                result.CreatedCount,
                result.SkippedCount),
            "确定");
    }
}
