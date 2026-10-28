using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Project 窗口右键文件夹 → <c>Assets/图集打包/为当前文件夹打包图集</c>。
/// <para>
/// 图集产物 <c>images_atlas.spriteatlas</c> 直接放在选中的文件夹下，支持一次处理多个选中的文件夹。
/// 打包规则复用 <see cref="AtlasPackager.PackAtlasForFolder"/>，与 AB 构建前的图集规则保持一致。
/// </para>
/// </summary>
public static class AtlasFolderContextMenu
{
    private const string MenuPath = "Assets/图集打包/为当前文件夹打包图集";

    [MenuItem(MenuPath, false, 2004)]
    private static void PackSelectedFolders()
    {
        List<string> folders = GetSelectedFolderPaths();
        if (folders.Count == 0)
        {
            Debug.LogError("[AtlasFolderContextMenu] 请先在 Project 窗口选中至少一个文件夹");
            return;
        }

        var summary = new StringBuilder();
        int packedCount = 0;
        int skippedCount = 0;
        string lastAtlasPath = null;

        for (int i = 0; i < folders.Count; i++)
        {
            AtlasPackager.FolderAtlasResult result = AtlasPackager.PackAtlasForFolder(folders[i]);
            if (!result.Packed)
            {
                skippedCount++;
                string reason = result.ImageCount > 0
                    ? "打包失败，详见 Console"
                    : "当前层没有图片（不递归子文件夹）";
                summary.AppendLine("× " + result.RequestedFolder + "：" + reason);
                continue;
            }

            packedCount++;
            lastAtlasPath = result.AtlasPath;
            summary.AppendLine("√ " + result.AtlasFolder);
            summary.AppendLine("    → " + Path.GetFileName(result.AtlasPath) + "，" + result.ImageCount + " 张图片");

            if (!string.Equals(result.AtlasFolder, result.RequestedFolder, StringComparison.Ordinal))
            {
                summary.AppendLine("    （当前层没有图片，已使用其 Images 子文件夹）");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 只处理一个图集时顺手选中并高亮它，方便直接在 Project 窗口确认产物位置。
        if (packedCount == 1 && !string.IsNullOrEmpty(lastAtlasPath))
        {
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(lastAtlasPath);
            if (atlas != null)
            {
                Selection.activeObject = atlas;
                EditorGUIUtility.PingObject(atlas);
            }
        }

        Debug.Log(string.Format(
            "[AtlasFolderContextMenu] 图集打包完成：成功 {0} 个，跳过 {1} 个。",
            packedCount,
            skippedCount));

        EditorUtility.DisplayDialog(
            "图集打包",
            string.Format("成功 {0} 个，跳过 {1} 个\n\n{2}", packedCount, skippedCount, summary.ToString().TrimEnd()),
            "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool PackSelectedFoldersValidate()
    {
        return GetSelectedFolderPaths().Count > 0;
    }

    /// <summary>取当前选中的文件夹；选中了图片/预制体时忽略，菜单只在有文件夹时可用。</summary>
    private static List<string> GetSelectedFolderPaths()
    {
        var folders = new List<string>();
        string[] guids = Selection.assetGUIDs;
        if (guids == null)
        {
            return folders;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            // 只处理工程内的目录：Packages 等虚拟路径无法生成图集资产。
            if (string.IsNullOrEmpty(path)
                || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            if (!folders.Contains(path))
            {
                folders.Add(path);
            }
        }

        return folders;
    }
}
