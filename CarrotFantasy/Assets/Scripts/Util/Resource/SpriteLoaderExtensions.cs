using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 为 SpriteRenderer 提供与 SpriteLoader 联动的便捷扩展方法。
    /// </summary>
    public static class SpriteLoaderExtensions
    {
        public static void SetSprite(this SpriteRenderer renderer, string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            if (renderer == null)
            {
                return;
            }

            EnsureLoader(renderer).SetSprite(bundleName, assetName, priority);
        }

        public static void SetAtlasSprite(this SpriteRenderer renderer, string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            if (renderer == null)
            {
                return;
            }

            EnsureLoader(renderer).SetAtlasSprite(bundleName, spriteName, priority);
        }

        public static void ReleaseSprite(this SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            SpriteLoader loader = renderer.GetComponent<SpriteLoader>();
            if (loader != null)
            {
                loader.ReleaseCurrent();
            }
        }

        private static SpriteLoader EnsureLoader(SpriteRenderer renderer)
        {
            SpriteLoader loader = renderer.GetComponent<SpriteLoader>();
            if (loader == null)
            {
                loader = renderer.gameObject.AddComponent<SpriteLoader>();
            }

            return loader;
        }
    }
}
