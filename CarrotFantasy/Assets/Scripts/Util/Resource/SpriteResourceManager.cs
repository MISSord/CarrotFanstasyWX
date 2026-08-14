using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 非图集 Sprite 加载入口（有感 Handle）。图集内精灵请走 <see cref="AtlasResourceManager"/>。
    /// </summary>
    public sealed class SpriteResourceManager
    {
        private static SpriteResourceManager _instance;
        public static SpriteResourceManager Instance => _instance ?? (_instance = new SpriteResourceManager());

        private SpriteResourceManager()
        {
        }

        public AssetLoadHandle Load(
            string bundleName,
            string assetName,
            Action<Sprite> onLoaded,
            LoadPriority priority = LoadPriority.Medium)
        {
            return AssetLoadManager.Instance.LoadAsset<Sprite>(
                bundleName,
                assetName,
                onLoaded,
                priority,
                "SpriteResourceManager.Load");
        }
    }
}
