using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 为Image/RawImage提供与UIImageLoader联动的便捷扩展方法。
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
