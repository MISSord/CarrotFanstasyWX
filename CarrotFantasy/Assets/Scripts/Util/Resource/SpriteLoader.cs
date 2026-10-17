using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到 <see cref="SpriteRenderer"/>，负责世界空间精灵请求、替换与释放。
    /// 图集走 <see cref="AtlasLoader"/>；普通 Sprite 走 <see cref="ImageLoader"/>。
    /// rawimages 包（Texture）不允许经此组件加载。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteLoader : MonoBehaviour
    {
        private SpriteRenderer targetRenderer;

        [Header("AB配置")]
        [SerializeField] private string defaultBundleName;
        [SerializeField] private string defaultAssetName;
        private LoadPriority defaultLoadPriority = LoadPriority.Medium;
        private bool clearSpriteOnRelease = true;

        private AssetBundleHandle<Sprite> _spriteHandle;
        private AtlasHandle _atlasHandle;
        private int _requestVersion;
        [SerializeField, HideInInspector]
        private int _lastRecordedSpriteInstanceId;

        private void Reset()
        {
            EnsureRendererBinding();
#if UNITY_EDITOR
            EditorRefreshBindingAndPath();
#endif
        }

        private void Awake()
        {
            EnsureRendererBinding();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorRefreshBindingAndPath();
        }

        public bool EditorRefreshBindingAndPath()
        {
            bool changed = EnsureRendererBinding();
            changed |= TryAutoRecordPathFromCurrentSprite();
            if (changed)
            {
                EditorUtility.SetDirty(this);
            }

            return changed;
        }

        private bool TryAutoRecordPathFromCurrentSprite()
        {
            if (targetRenderer == null || targetRenderer.sprite == null)
            {
                bool cleared = false;
                if (_lastRecordedSpriteInstanceId != 0)
                {
                    _lastRecordedSpriteInstanceId = 0;
                    cleared = true;
                }

                if (!string.IsNullOrEmpty(defaultBundleName))
                {
                    defaultBundleName = string.Empty;
                    cleared = true;
                }

                if (!string.IsNullOrEmpty(defaultAssetName))
                {
                    defaultAssetName = string.Empty;
                    cleared = true;
                }

                return cleared;
            }

            Sprite source = targetRenderer.sprite;
            int spriteId = source.GetInstanceID();
            bool hasDefaultPath = !string.IsNullOrEmpty(defaultBundleName) && !string.IsNullOrEmpty(defaultAssetName);
            if (_lastRecordedSpriteInstanceId == spriteId && hasDefaultPath)
            {
                return false;
            }

            string spriteAssetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(spriteAssetPath))
            {
                return false;
            }

            AssetImporter importer = AssetImporter.GetAtPath(spriteAssetPath);
            if (importer == null || string.IsNullOrEmpty(importer.assetBundleName))
            {
                return false;
            }

            bool changed = false;
            string newBundleName = importer.assetBundleName.ToLowerInvariant();
            if (!string.Equals(defaultBundleName, newBundleName))
            {
                defaultBundleName = newBundleName;
                changed = true;
            }

            string assetName = source.name;
            if (!string.Equals(defaultAssetName, assetName))
            {
                defaultAssetName = assetName;
                changed = true;
            }

            _lastRecordedSpriteInstanceId = spriteId;
            return changed;
        }
#endif

        private void OnDestroy()
        {
            ReleaseCurrent();
        }

        private void OnEnable()
        {
            if (_spriteHandle != null || _atlasHandle != null)
            {
                return;
            }

            if (string.IsNullOrEmpty(defaultBundleName) || string.IsNullOrEmpty(defaultAssetName))
            {
                return;
            }

            SetSprite(defaultBundleName, defaultAssetName, defaultLoadPriority);
        }

        /// <summary>从图集取图；bundleName 为图集 AB 包名。引用由本组件自动 Release。</summary>
        public void SetAtlasSprite(string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = spriteName;
            defaultLoadPriority = priority;
            EnsureRendererBinding();

            if (targetRenderer == null)
            {
                GameLogController.Warning(
                    "SetAtlasSprite 需要 SpriteRenderer: " + gameObject.name,
                    "SpriteLoader");
                return;
            }

            _atlasHandle = AtlasLoader.Instance.Load(bundleName, spriteName, priority);
            _atlasHandle.Completed += sprite =>
            {
                if (currentVersion != _requestVersion || targetRenderer == null)
                {
                    return;
                }

                targetRenderer.sprite = sprite;
            };
        }

        /// <summary>
        /// 按 AB 路径加载 Sprite。图集包自动转 Atlas；rawimages 包为 Texture 专用，SpriteRenderer 应拒绝。
        /// </summary>
        public void SetSprite(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            EnsureRendererBinding();

            if (AtlasLoader.Instance.IsAtlasBundle(bundleName))
            {
                SetAtlasSprite(bundleName, assetName, priority);
                return;
            }

            if (IsRawImageBundle(bundleName))
            {
                GameLogController.Error(
                    "SpriteLoader 不允许加载 rawimages 包（Texture）：" + bundleName + "/" + assetName + "，请改用图集或 ui/sprites 包。",
                    "SpriteLoader");
                return;
            }

            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = assetName;
            defaultLoadPriority = priority;

            if (targetRenderer == null)
            {
                GameLogController.Warning(
                    "SetSprite 需要 SpriteRenderer: " + gameObject.name,
                    "SpriteLoader");
                return;
            }

            _spriteHandle = ImageLoader.Instance.LoadSprite(bundleName, assetName, priority);
            _spriteHandle.Completed += sprite =>
            {
                if (currentVersion != _requestVersion || targetRenderer == null)
                {
                    return;
                }

                targetRenderer.sprite = sprite;
            };
        }

        public void ReleaseCurrent()
        {
            if (_atlasHandle != null)
            {
                _atlasHandle.Release();
                _atlasHandle = null;
            }

            if (_spriteHandle != null)
            {
                _spriteHandle.Release();
                _spriteHandle = null;
            }

            if (clearSpriteOnRelease && targetRenderer != null)
            {
                targetRenderer.sprite = null;
            }
        }

        private static bool IsRawImageBundle(string bundleName)
        {
            return !string.IsNullOrEmpty(bundleName) &&
                   bundleName.StartsWith("ui/rawimages/", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool EnsureRendererBinding()
        {
            if (targetRenderer != null)
            {
                return false;
            }

            targetRenderer = GetComponent<SpriteRenderer>();
            return targetRenderer != null;
        }
    }
}
