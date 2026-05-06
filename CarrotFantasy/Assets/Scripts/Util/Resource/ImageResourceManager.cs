using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 图片资源入口（Sprite / Texture），内部委托 <see cref="AssetLoadManager"/>。
    /// </summary>
    public sealed class ImageResourceManager
    {
        private static ImageResourceManager _instance;
        public static ImageResourceManager Instance => _instance ?? (_instance = new ImageResourceManager());

        private ImageResourceManager()
        {
        }

        public AssetLoadHandle LoadSprite(string bundleName, string assetName, Action<Sprite> onLoaded, LoadPriority priority = LoadPriority.Medium)
        {
            return AssetLoadManager.Instance.LoadAsset<Sprite>(bundleName, assetName, onLoaded, priority, "LoadSprite");
        }

        public AssetLoadHandle LoadTexture(string bundleName, string assetName, Action<Texture> onLoaded, LoadPriority priority = LoadPriority.Medium)
        {
            return AssetLoadManager.Instance.LoadAsset<Texture>(bundleName, assetName, onLoaded, priority, "LoadTexture");
        }
    }
}
