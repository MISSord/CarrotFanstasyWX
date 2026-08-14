using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// Texture 加载入口（RawImage / 未来 3D 贴图等，有感 Handle）。
    /// </summary>
    public sealed class TextureResourceManager
    {
        private static TextureResourceManager _instance;
        public static TextureResourceManager Instance => _instance ?? (_instance = new TextureResourceManager());

        private TextureResourceManager()
        {
        }

        public AssetLoadHandle Load(
            string bundleName,
            string assetName,
            Action<Texture> onLoaded,
            LoadPriority priority = LoadPriority.Medium)
        {
            return AssetLoadManager.Instance.LoadAsset<Texture>(
                bundleName,
                assetName,
                onLoaded,
                priority,
                "TextureResourceManager.Load");
        }
    }
}
