using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 驱动各加载器的空闲计时，并在下一帧统一卸包。
    /// </summary>
    public static class AssetBundleLoaders
    {
        public const string LogTag = "AssetBundleLoader";
        private const int BundlesPerFrame = 8;
        private static bool _sweepPending;
        private static int _sweepFrame = -1;
        private static bool _idleReleased;

        public static void Init()
        {
            _ = PrefabLoader.Instance;
            _ = ImageLoader.Instance;
            _ = AudioLoader.Instance;
            _ = TextLoader.Instance;
        }

        public static void Tick()
        {
            PrefabLoader.Instance.Tick();
            ImageLoader.Instance.Tick();
            AudioLoader.Instance.Tick();
            TextLoader.Instance.Tick();

            if (AssetBundleManager.Instance != null)
            {
                AssetBundleManager.Instance.UnloadUnreferencedBundles(1);
            }

            if (!_sweepPending || Time.frameCount < _sweepFrame)
            {
                return;
            }

            if (!_idleReleased)
            {
                PrefabLoader.Instance.ReleaseIdle(true);
                ImageLoader.Instance.ReleaseIdle(true);
                AudioLoader.Instance.ReleaseIdle(true);
                TextLoader.Instance.ReleaseIdle(true);
                _idleReleased = true;
            }

            int unloaded = 0;
            if (AssetBundleManager.Instance != null)
            {
                unloaded = AssetBundleManager.Instance.UnloadUnreferencedBundles(BundlesPerFrame);
            }

            if (unloaded < BundlesPerFrame)
            {
                _sweepPending = false;
                _idleReleased = false;
            }
        }

        /// <summary>等 Destroy 在帧末生效后，下一帧再归还空闲资源并卸包。</summary>
        public static void RequestSweep()
        {
            _sweepPending = true;
            _sweepFrame = Time.frameCount + 1;
            _idleReleased = false;
        }
    }

    public sealed class PrefabLoader
    {
        public static PrefabLoader Instance { get; } = new PrefabLoader();

        private readonly AssetBundleCache<GameObject> _cache;

        private PrefabLoader()
        {
            _cache = new AssetBundleCache<GameObject>("PrefabLoader", new AssetBundleCachePolicy
            {
                IdleSeconds = 120f,
                RememberFailure = true,
                FailureRememberSeconds = 5f
            });
        }

        public AssetBundleHandle<GameObject> Load(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            return _cache.Load(bundleName, assetName, priority);
        }

        public void Tick()
        {
            _cache.ReleaseIdle(false);
        }

        public int ReleaseIdle(bool ignoreDelay)
        {
            return _cache.ReleaseIdle(ignoreDelay);
        }

        public string Explain(string bundleName)
        {
            return _cache.Explain(bundleName);
        }
    }

    public sealed class ImageLoader
    {
        public static ImageLoader Instance { get; } = new ImageLoader();

        private readonly AssetBundleCache<Sprite> _sprites;
        private readonly AssetBundleCache<Texture> _textures;

        private ImageLoader()
        {
            AssetBundleCachePolicy imagePolicy = new AssetBundleCachePolicy
            {
                IdleSeconds = 15f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            };
            _sprites = new AssetBundleCache<Sprite>("ImageLoader.Sprite", imagePolicy);
            _textures = new AssetBundleCache<Texture>("ImageLoader.Texture", imagePolicy);
        }

        public AssetBundleHandle<Sprite> LoadSprite(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            return _sprites.Load(bundleName, assetName, priority);
        }

        public AssetBundleHandle<Texture> LoadTexture(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            return _textures.Load(bundleName, assetName, priority);
        }

        public void Tick()
        {
            _sprites.ReleaseIdle(false);
            _textures.ReleaseIdle(false);
        }

        public int ReleaseIdle(bool ignoreDelay)
        {
            return _sprites.ReleaseIdle(ignoreDelay) + _textures.ReleaseIdle(ignoreDelay);
        }

        public string Explain(string bundleName)
        {
            return _sprites.Explain(bundleName) + _textures.Explain(bundleName);
        }
    }

    public sealed class AudioLoader
    {
        public static AudioLoader Instance { get; } = new AudioLoader();

        private readonly AssetBundleCache<AudioClip> _cache;

        private AudioLoader()
        {
            _cache = new AssetBundleCache<AudioClip>("AudioLoader", new AssetBundleCachePolicy
            {
                IdleSeconds = 30f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            });
        }

        public AssetBundleHandle<AudioClip> Load(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            return _cache.Load(bundleName, assetName, priority);
        }

        public void Tick()
        {
            _cache.ReleaseIdle(false);
        }

        public int ReleaseIdle(bool ignoreDelay)
        {
            return _cache.ReleaseIdle(ignoreDelay);
        }

        public string Explain(string bundleName)
        {
            return _cache.Explain(bundleName);
        }
    }

    public sealed class TextLoader
    {
        public static TextLoader Instance { get; } = new TextLoader();

        private readonly AssetBundleCache<TextAsset> _cache;

        private TextLoader()
        {
            _cache = new AssetBundleCache<TextAsset>("TextLoader", new AssetBundleCachePolicy
            {
                IdleSeconds = 0f,
                RememberFailure = false,
                FailureRememberSeconds = 0f
            });
        }

        public AssetBundleHandle<TextAsset> Load(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            return _cache.Load(bundleName, assetName, priority);
        }

        public void Tick()
        {
            _cache.ReleaseIdle(false);
        }

        public int ReleaseIdle(bool ignoreDelay)
        {
            return _cache.ReleaseIdle(ignoreDelay);
        }

        public string Explain(string bundleName)
        {
            return _cache.Explain(bundleName);
        }
    }
}
