using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 为 Image/RawImage 提供与 UIImageLoader 联动的便捷扩展方法。
    /// </summary>
    public static class UIImageLoaderExtensions
    {
        /// <summary>从图集取图。UI Image 不走单张 ImageLoader。</summary>
        public static void SetSprite(this Image image, string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            image.SetAtlasSprite(bundleName, assetName, priority);
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

            RawImageTextureSource source = rawImage.GetComponent<RawImageTextureSource>();
            if (source == null)
            {
                source = rawImage.gameObject.AddComponent<RawImageTextureSource>();
            }

            source.SetTexture(rawImage, bundleName, assetName, priority);
        }
    }

    /// <summary>RawImage 的单张贴图。不放进 UIImageLoader，避免和图集加载混在一起。</summary>
    sealed class RawImageTextureSource : MonoBehaviour
    {
        AssetBundleHandle<Texture> _handle;
        int _version;

        public void SetTexture(RawImage rawImage, string bundleName, string assetName, LoadPriority priority)
        {
            _version++;
            int version = _version;
            if (_handle != null)
            {
                _handle.Release();
                _handle = null;
            }

            _handle = ImageLoader.Instance.LoadTexture(bundleName, assetName, priority);
            _handle.Completed += texture =>
            {
                if (version != _version || rawImage == null)
                {
                    return;
                }

                rawImage.texture = texture;
            };
        }

        void OnDestroy()
        {
            if (_handle != null)
            {
                _handle.Release();
                _handle = null;
            }
        }
    }
}
