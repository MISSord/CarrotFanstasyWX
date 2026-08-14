using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 为 Image/RawImage 提供与 UIImageLoader 联动的便捷扩展方法。
    /// </summary>
    public static class UIImageLoaderExtensions
    {
        public static void SetSprite(this Image image, string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            if (image == null)
            {
                return;
            }

            UIImageLoader loader = image.GetComponent<UIImageLoader>();
            if (loader == null)
            {
                loader = image.gameObject.AddComponent<UIImageLoader>();
            }

            loader.SetSprite(bundleName, assetName, priority);
        }

        /// <summary>从图集取图；bundleName 为图集 AB 包名。业务无需处理引用计数。</summary>
        public static void SetAtlasSprite(this Image image, string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            if (image == null)
            {
                return;
            }

            UIImageLoader loader = image.GetComponent<UIImageLoader>();
            if (loader == null)
            {
                loader = image.gameObject.AddComponent<UIImageLoader>();
            }

            loader.SetAtlasSprite(bundleName, spriteName, priority);
        }

        public static void SetTexture(this RawImage rawImage, string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            if (rawImage == null)
            {
                return;
            }

            UIImageLoader loader = rawImage.GetComponent<UIImageLoader>();
            if (loader == null)
            {
                loader = rawImage.gameObject.AddComponent<UIImageLoader>();
            }

            loader.SetSprite(bundleName, assetName, priority);
        }
    }
}
