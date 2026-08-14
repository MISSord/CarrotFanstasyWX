using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到 <see cref="SpriteRenderer"/>，负责世界空间精灵请求、替换与释放。
    /// 图集走 <see cref="AtlasResourceManager"/>；普通 Sprite 走 <see cref="SpriteResourceManager"/>。
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

        private AssetLoadHandle _currentHandle = AssetLoadHandle.Invalid;
        private int _atlasToken = AtlasResourceManager.InvalidToken;
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
            if (_currentHandle.IsValid || _atlasToken != AtlasResourceManager.InvalidToken)
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

            _atlasToken = AtlasResourceManager.Instance.AcquireSprite(
                bundleName,
                spriteName,
                sprite =>
                {
                    if (currentVersion != _requestVersion || targetRenderer == null)
                    {
                        return;
                    }

                    targetRenderer.sprite = sprite;
                },
                priority);
        }

        /// <summary>
        /// 按 AB 路径加载 Sprite。图集包自动转 Atlas；rawimages 包为 Texture 专用，SpriteRenderer 应拒绝。
        /// </summary>
        public void SetSprite(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            EnsureRendererBinding();

            if (AtlasResourceManager.Instance.IsAtlasBundle(bundleName))
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

            _currentHandle = SpriteResourceManager.Instance.Load(
                bundleName,
                assetName,
                sprite =>
                {
                    if (currentVersion != _requestVersion || targetRenderer == null)
                    {
                        return;
                    }

                    targetRenderer.sprite = sprite;
                },
                priority);
        }

        public void ReleaseCurrent()
        {
            if (_atlasToken != AtlasResourceManager.InvalidToken)
            {
                AtlasResourceManager.Instance.Release(_atlasToken);
                _atlasToken = AtlasResourceManager.InvalidToken;
            }

            if (_currentHandle.IsValid)
            {
                _currentHandle.Dispose();
                _currentHandle = AssetLoadHandle.Invalid;
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
