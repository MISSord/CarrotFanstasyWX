using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 把一张已切好 Sprite 的大图，按切片信息导出成一张张小 PNG。
/// <para>
/// 2022+ 起 <c>TextureImporter.spritesheet</c> 已**移除**（不只是过时），
/// 原实现取不到切片。这里改用官方替代方案
/// <see cref="ISpriteEditorDataProvider"/> 读取切片矩形与名字。
/// </para>
/// <para>
/// 用法：Project 里选中一张 Texture2D → 右键 Assets/ImageSlicer/Process to Sprites。
/// 要求该图已在 Sprite Editor 里切好；未开启 Read/Write 时会临时打开并在结束后恢复。
/// </para>
/// </summary>
public static class ImageSlicer
{
    [MenuItem("Assets/ImageSlicer/Process to Sprites")]
    private static void ProcessToSprite()
    {
        Texture2D image = Selection.activeObject as Texture2D;
        if (image == null)
        {
            Debug.LogError("[ImageSlicer] 请先在 Project 里选中一张 Texture2D");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(image);
        string rootPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(rootPath))
        {
            Debug.LogError("[ImageSlicer] 无法解析资源所在目录: " + assetPath);
            return;
        }

        TextureImporter texImp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (texImp == null)
        {
            Debug.LogError("[ImageSlicer] 该资源不是 TextureImporter 管理的贴图: " + assetPath);
            return;
        }

        // 读取切片：旧 API（spritesheet）已移除，必须走数据提供者。
        SpriteRect[] spriteRects = ReadSpriteRects(texImp);
        if (spriteRects == null || spriteRects.Length == 0)
        {
            Debug.LogWarning("[ImageSlicer] 这张图没有切片数据，请先在 Sprite Editor 里切好: " + assetPath);
            return;
        }

        string folderPath = rootPath + "/" + image.name;
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(rootPath, image.name);
        }

        // 读像素要求 Read/Write 打开，否则 GetPixels 会抛异常；用完恢复原设置。
        bool wasReadable = texImp.isReadable;
        if (!wasReadable)
        {
            texImp.isReadable = true;
            texImp.SaveAndReimport();
        }

        try
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (source == null)
            {
                Debug.LogError("[ImageSlicer] 无法重新加载贴图: " + assetPath);
                return;
            }

            int written = 0;
            for (int i = 0; i < spriteRects.Length; i++)
            {
                SpriteRect spriteRect = spriteRects[i];
                Rect rect = spriteRect.rect;

                int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
                int x = Mathf.RoundToInt(rect.x);
                int y = Mathf.RoundToInt(rect.y);

                // 越界时跳过，避免 GetPixels 直接抛异常中断整批导出。
                if (x < 0 || y < 0 || x + width > source.width || y + height > source.height)
                {
                    Debug.LogWarning(
                        $"[ImageSlicer] 切片超出贴图范围，已跳过: {spriteRect.name} " +
                        $"(x={x}, y={y}, w={width}, h={height}, texture={source.width}x{source.height})");
                    continue;
                }

                Texture2D slice = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    slice.SetPixels(source.GetPixels(x, y, width, height));
                    slice.Apply();

                    byte[] pngData = slice.EncodeToPNG();
                    File.WriteAllBytes(folderPath + "/" + spriteRect.name + ".PNG", pngData);
                    written++;
                }
                finally
                {
                    Object.DestroyImmediate(slice);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ImageSlicer] 导出完成：{written}/{spriteRects.Length} 张 → {folderPath}");
        }
        finally
        {
            if (!wasReadable)
            {
                texImp.isReadable = false;
                texImp.SaveAndReimport();
            }
        }
    }

    /// <summary>用 2D Sprite 包的数据提供者读取切片信息。</summary>
    private static SpriteRect[] ReadSpriteRects(TextureImporter importer)
    {
        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
        {
            return null;
        }

        provider.InitSpriteEditorDataProvider();
        return provider.GetSpriteRects();
    }
}
