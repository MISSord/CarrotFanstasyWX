using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到 Image/RawImage，负责图片请求、替换与释放。
    /// 图集精灵经 <see cref="AtlasResourceManager"/> 计数；非图集 Sprite/Texture 经对应 Manager + Handle。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIImageLoader : MonoBehaviour
    {
        private Image targetImage;
        private RawImage targetRawImage;

        [Header("AB配置")]
        [SerializeField] private string defaultBundleName;
        [SerializeField] private string defaultAssetName;
        private LoadPriority defaultLoadPriority = LoadPriority.Medium;
        private bool clearSpriteOnRelease = true;

        private AssetLoadHandle _currentHandle = AssetLoadHandle.Invalid;
        private int _atlasToken = AtlasResourceManager.InvalidToken;
        private int _requestVersion = 0;
        [SerializeField, HideInInspector]
        private int _lastRecordedSpriteInstanceId;

        private void Reset()
        {
            EnsureGraphicBinding();
#if UNITY_EDITOR
            EditorRefreshBindingAndPath();
#endif
        }

        private void Awake()
        {
            EnsureGraphicBinding();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorRefreshBindingAndPath();
        }

        /// <summary>
        /// 编辑器下供外部调用：自动绑定 Image/RawImage，并从当前贴图刷新 AB 字段。
        /// </summary>
        public bool EditorRefreshBindingAndPath()
        {
            bool changed = false;
            changed |= EnsureGraphicBinding();
            changed |= TryAutoRecordPathFromCurrentGraphic();
            if (changed)
            {
                EditorUtility.SetDirty(this);
            }
            return changed;
        }

        private bool TryAutoRecordPathFromCurrentGraphic()
        {
            UnityEngine.Object sourceAsset = null;
            string assetName = string.Empty;
            if (targetImage != null && targetImage.sprite != null)
            {
                sourceAsset = targetImage.sprite;
                assetName = targetImage.sprite.name;
            }
            else if (targetRawImage != null && targetRawImage.texture != null)
            {
                sourceAsset = targetRawImage.texture;
                assetName = targetRawImage.texture.name;
            }

            if (sourceAsset == null)
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

            int spriteId = sourceAsset.GetInstanceID();
            bool hasDefaultPath = !string.IsNullOrEmpty(defaultBundleName) && !string.IsNullOrEmpty(defaultAssetName);
            if (_lastRecordedSpriteInstanceId == spriteId && hasDefaultPath)
            {
                return false;
            }

            string spriteAssetPath = AssetDatabase.GetAssetPath(sourceAsset);
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

            if (!string.Equals(defaultAssetName, assetName))
            {
                defaultAssetName = assetName;
                changed = true;
            }

            defaultBundleName = importer.assetBundleName.ToLowerInvariant();
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

        /// <summary>
        /// 从图集取图。<paramref name="bundleName"/> 为图集 AB 包名（含 images_atlas）。
        /// 引用由本组件在换图/销毁时自动 Release，业务无需关心计数。
        /// </summary>
        public void SetAtlasSprite(string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = spriteName;
            defaultLoadPriority = priority;
            EnsureGraphicBinding();

            if (targetImage == null)
            {
                GameLogController.Warning(
                    "SetAtlasSprite 需要 Image 组件: " + gameObject.name,
                    "UIImageLoader");
                return;
            }

            if (targetRawImage != null)
            {
                GameLogController.Error(
                    "SetAtlasSprite 不允许用于 RawImage：图集 Sprite 只能赋给 Image。",
                    "UIImageLoader");
                return;
            }

            _atlasToken = AtlasResourceManager.Instance.AcquireSprite(
                bundleName,
                spriteName,
                sprite =>
                {
                    if (currentVersion != _requestVersion || targetImage == null)
                    {
                        return;
                    }

                    targetImage.sprite = sprite;
                },
                priority);
        }

        /// <summary>
        /// 按 AB 路径加载。若目标为图集包则自动转 <see cref="SetAtlasSprite"/>。
        /// Image 只允许 Sprite（图集/普通 Sprite 包）；RawImage 只允许 rawimages 包（Texture）。
        /// </summary>
        public void SetSprite(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            EnsureGraphicBinding();

            if (targetImage != null && AtlasResourceManager.Instance.IsAtlasBundle(bundleName))
            {
                SetAtlasSprite(bundleName, assetName, priority);
                return;
            }

            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = assetName;
            defaultLoadPriority = priority;

            if (targetImage != null)
            {
                if (IsRawImageBundle(bundleName))
                {
                    GameLogController.Error(
                        "Image 不允许加载 rawimages 包（Texture）：" + bundleName + "/" + assetName + "，请改用图集或普通 Sprite 包。",
                        "UIImageLoader");
                    return;
                }

                _currentHandle = SpriteResourceManager.Instance.Load(
                    bundleName,
                    assetName,
                    sprite =>
                    {
                        if (currentVersion != _requestVersion || targetImage == null)
                        {
                            return;
                        }

                        targetImage.sprite = sprite;
                    },
                    priority);
                return;
            }

            if (targetRawImage != null)
            {
                if (!IsRawImageBundle(bundleName))
                {
                    GameLogController.Error(
                        "RawImage 只允许加载 rawimages 包（Texture）：" + bundleName + "/" + assetName,
                        "UIImageLoader");
                    return;
                }

                _currentHandle = TextureResourceManager.Instance.Load(
                    bundleName,
                    assetName,
                    texture =>
                    {
                        if (currentVersion != _requestVersion || targetRawImage == null)
                        {
                            return;
                        }

                        targetRawImage.texture = texture;
                    },
                    priority);
            }
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

            if (clearSpriteOnRelease && targetImage != null)
            {
                targetImage.sprite = null;
            }

            if (clearSpriteOnRelease && targetRawImage != null)
            {
                targetRawImage.texture = null;
            }
        }

        private bool EnsureGraphicBinding()
        {
            bool changed = false;
            if (targetImage == null)
            {
                Image img = GetComponent<Image>();
                if (img != null)
                {
                    targetImage = img;
                    changed = true;
                }
            }

            if (targetRawImage == null)
            {
                RawImage raw = GetComponent<RawImage>();
                if (raw != null)
                {
                    targetRawImage = raw;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsRawImageBundle(string bundleName)
        {
            return !string.IsNullOrEmpty(bundleName) &&
                   bundleName.StartsWith("ui/rawimages/", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
