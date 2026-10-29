using System;
using UnityEditor;
using UnityEngine;

namespace CarrotFantasy.Editor
{
    /// <summary>
    /// <c>Assets/Game/UI/RawImages</c> 下贴图统一按 <see cref="TextureImporterType.Default"/> 导入。
    /// <para>
    /// 这个目录的消费方是 <c>RawImage</c>（<see cref="RawImageExtensions"/> / <see cref="RawImageLoader"/>），
    /// 拿的是 <c>Texture2D</c>；按 Sprite 导入会额外生成 Sprite 子资源、参与精灵/图集相关导入开销，
    /// 对 RawImage 毫无用处。
    /// </para>
    /// <para>
    /// 目录里原先混着 82 张被 <c>Image</c>/<c>SpriteRenderer</c> 以 <c>m_Sprite</c> 引用的图
    /// （HelpPanel、GameMap Items、BossModel 等），已迁到 <c>Assets/Game/UI/Sprites</c>
    /// （对应包名 <c>ui/sprites/...</c>，也是 <c>SpriteLoader</c> 允许的来源）。
    /// 因此本规则不再需要例外清单；往 RawImages 新增图片即按 Texture 导入。
    /// </para>
    /// </summary>
    public sealed class RawImageTextureImportRule : AssetPostprocessor
    {
        public const string Root = "Assets/Game/UI/RawImages/";

        void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter importer))
            {
                return;
            }

            if (!Normalize(assetPath).StartsWith(Root, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyTextureSettings(importer);
        }

        /// <summary>RawImage 贴图的导入设置：2D、无 mipmap、Clamp、不生成 Sprite 子资源。</summary>
        public static void ApplyTextureSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
