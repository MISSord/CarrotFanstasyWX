using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到Image/RawImage节点，负责图片资源请求、替换和自动释放。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIImageLoader : MonoBehaviour
    {
        private Image targetImage;
        private RawImage targetRawImage;

        [Header("AB配置")]
        [SerializeField] private string defaultBundleName;
        [SerializeField] private string defaultAssetName;
        //[SerializeField] 
        private LoadPriority defaultLoadPriority = LoadPriority.Medium;
        //[SerializeField] 
        //private bool autoLoadOnEnable = true;

        //[Header("生命周期")]
        //[SerializeField]
        //private bool releaseOnDisable = true;
        //[SerializeField] 
        private bool clearSpriteOnRelease = true;

        private AssetLoadHandle _currentHandle = AssetLoadHandle.Invalid;
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
        /// 编辑器下供外部调用：自动绑定Image/RawImage，并从当前贴图刷新AB字段。
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
                bool wasRecorded = _lastRecordedSpriteInstanceId != 0;
                _lastRecordedSpriteInstanceId = 0;
                return wasRecorded;
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

        //private void OnDisable()
        //{
        //    if (releaseOnDisable)
        //    {
        //        ReleaseCurrent();
        //    }
        //}

        private void OnDestroy()
        {
            ReleaseCurrent();
        }

        private void OnEnable()
        {
            //if (!autoLoadOnEnable)
            //{
            //    return;
            //}

            // 未释放的情况下无需重复加载（比如 releaseOnDisable=false 时反复启用组件）。
            if (_currentHandle.IsValid)
            {
                return;
            }

            if (string.IsNullOrEmpty(defaultBundleName) || string.IsNullOrEmpty(defaultAssetName))
            {
                return;
            }

            SetSprite(defaultBundleName, defaultAssetName, defaultLoadPriority);
        }

        public void SetSprite(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = assetName;
            defaultLoadPriority = priority;
            EnsureGraphicBinding();

            if (targetImage != null)
            {
                _currentHandle = ImageResourceManager.Instance.LoadSprite(
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
                _currentHandle = ImageResourceManager.Instance.LoadTexture(
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

        //public void ReloadDefault()
        //{
        //    if (string.IsNullOrEmpty(defaultBundleName) || string.IsNullOrEmpty(defaultAssetName))
        //    {
        //        return;
        //    }

        //    SetSprite(defaultBundleName, defaultAssetName, defaultLoadPriority);
        //}

        public void ReleaseCurrent()
        {
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
    }
}
