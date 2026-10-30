using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到 Image，只负责从图集取 Sprite 并在换图或销毁时释放引用。
    /// 单张 Sprite / Texture 不经过这里，由调用方直接使用 <see cref="ImageLoader"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIImageLoader : MonoBehaviour
    {
        private Image targetImage;

        [Header("AB配置")]
        [SerializeField] private string defaultBundleName;
        [SerializeField] private string defaultAssetName;
        private LoadPriority defaultLoadPriority = LoadPriority.Medium;
        private bool clearSpriteOnRelease = true;

        private AtlasHandle _atlasHandle;
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
            // 播放中预制体来自 AB，图集 Sprite 引用经常还是空的。
            // 这时按「当前没有图就清空包名」会抹掉已序列化的 AB 字段，
            // OnEnable 看到空字段就不再加载。Testing 模式因此整图空白。
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

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

            // 包名能不能加载，优先于「已记录就跳过」判断：
            // 否则先前记错的路径（如 ui/rawimages/xxx）永远不会被纠正。
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

            // 只有图集包能经 AtlasLoader 加载；ui/sprites 这类「直出精灵」的包名
            // 传进来会在运行时加载失败并把 sprite 置空，所以这里不记录，反而清空字段，
            // 让 prefab 上序列化的 Sprite 直接生效。
            if (importer.assetBundleName.IndexOf("images_atlas", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return ClearRecordedPath();
            }

            // 注意：把图片移动到别的目录（GUID / Sprite 对象不变）只会换掉 importer 的包名，
            // InstanceID 仍是旧值。所以不能只凭 InstanceID 命中就跳过，必须同时确认
            // 当前包名、资源名与已记录的一致；否则移动过目录的图会永远保留旧 AB 路径。
            int spriteId = sourceAsset.GetInstanceID();
            string newBundleName = importer.assetBundleName.ToLowerInvariant();
            if (_lastRecordedSpriteInstanceId == spriteId
                && string.Equals(defaultBundleName, newBundleName, System.StringComparison.Ordinal)
                && string.Equals(defaultAssetName, assetName, System.StringComparison.Ordinal))
            {
                return false;
            }

            bool changed = false;
            if (!string.Equals(defaultBundleName, newBundleName, System.StringComparison.Ordinal))
            {
                defaultBundleName = newBundleName;
                changed = true;
            }

            if (!string.Equals(defaultAssetName, assetName, System.StringComparison.Ordinal))
            {
                defaultAssetName = assetName;
                changed = true;
            }

            _lastRecordedSpriteInstanceId = spriteId;
            return changed;
        }

        /// <summary>清空已记录的 AB 路径（该图不适合走 AB 加载）。返回是否发生过变更。</summary>
        private bool ClearRecordedPath()
        {
            bool cleared = false;
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

            if (_lastRecordedSpriteInstanceId != 0)
            {
                _lastRecordedSpriteInstanceId = 0;
                cleared = true;
            }

            return cleared;
        }
#endif

        private void OnDestroy()
        {
            ReleaseCurrent();
        }

        private void OnEnable()
        {
            if (_atlasHandle != null)
            {
                return;
            }

            if (string.IsNullOrEmpty(defaultBundleName) || string.IsNullOrEmpty(defaultAssetName))
            {
                return;
            }

            SetAtlasSprite(defaultBundleName, defaultAssetName, defaultLoadPriority);
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

            _atlasHandle = AtlasLoader.Instance.Load(bundleName, spriteName, priority);
            _atlasHandle.Completed += sprite =>
            {
                if (currentVersion != _requestVersion || targetImage == null)
                {
                    return;
                }

                targetImage.sprite = sprite;
            };
        }

        public void ReleaseCurrent()
        {
            if (_atlasHandle != null)
            {
                _atlasHandle.Release();
                _atlasHandle = null;
            }

            if (clearSpriteOnRelease && targetImage != null)
            {
                targetImage.sprite = null;
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

            return changed;
        }
    }
}
