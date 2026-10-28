using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarrotFantasy
{
    /// <summary>
    /// 绑定到 RawImage，只负责加载单张贴图并在换图或销毁时释放引用。
    /// <para>
    /// 与 <see cref="UIImageLoader"/> 设计一致：那个面向 Image + 图集精灵，这个面向 RawImage + 单张贴图。
    /// 两者刻意不合并——图集与单图的引用计数、释放时机不同，混在一起容易互相干扰。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class RawImageLoader : MonoBehaviour
    {
        private RawImage targetRawImage;

        [Header("AB配置")]
        [SerializeField] private string defaultBundleName;
        [SerializeField] private string defaultAssetName;
        private LoadPriority defaultLoadPriority = LoadPriority.Medium;
        private bool clearTextureOnRelease = true;

        private AssetBundleHandle<Texture> _textureHandle;
        private int _requestVersion = 0;
        [SerializeField, HideInInspector]
        private int _lastRecordedTextureInstanceId;

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
        /// 编辑器下供外部调用：自动绑定 RawImage，并从当前贴图刷新 AB 字段。
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
            if (targetRawImage != null && targetRawImage.texture != null)
            {
                sourceAsset = targetRawImage.texture;
                assetName = targetRawImage.texture.name;
            }
            if (sourceAsset == null)
            {
                bool cleared = false;
                if (_lastRecordedTextureInstanceId != 0)
                {
                    _lastRecordedTextureInstanceId = 0;
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
            // 否则先前记错的路径永远不会被纠正。
            string assetPath = AssetDatabase.GetAssetPath(sourceAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || string.IsNullOrEmpty(importer.assetBundleName))
            {
                return false;
            }

            // 本组件按 Texture 加载，只认 rawimages 包；图集包（images_atlas）里的
            // 贴图是图集内页，单独取没有意义，直接不记录。
            if (importer.assetBundleName.IndexOf("/rawimages/", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return ClearRecordedPath();
            }

            // 贴图移动目录后 GUID / Texture 对象不变，InstanceID 命中不代表包名没变，
            // 必须比对当前包名与资源名，否则旧 AB 路径不会被纠正。
            int textureId = sourceAsset.GetInstanceID();
            string newBundleName = importer.assetBundleName.ToLowerInvariant();
            if (_lastRecordedTextureInstanceId == textureId
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

            _lastRecordedTextureInstanceId = textureId;
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

            if (_lastRecordedTextureInstanceId != 0)
            {
                _lastRecordedTextureInstanceId = 0;
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
            if (_textureHandle != null)
            {
                return;
            }

            if (string.IsNullOrEmpty(defaultBundleName) || string.IsNullOrEmpty(defaultAssetName))
            {
                return;
            }

            SetTexture(defaultBundleName, defaultAssetName, defaultLoadPriority);
        }

        /// <summary>
        /// 加载单张贴图。<paramref name="bundleName"/> 为图片自身的 AB 包名（常见为 ui/rawimages/xxx）。
        /// 引用由本组件在换图/销毁时自动 Release，业务无需关心计数。
        /// </summary>
        public void SetTexture(string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            _requestVersion++;
            int currentVersion = _requestVersion;

            ReleaseCurrent();
            defaultBundleName = bundleName;
            defaultAssetName = assetName;
            defaultLoadPriority = priority;
            EnsureGraphicBinding();

            if (targetRawImage == null)
            {
                GameLogController.Warning(
                    "SetTexture 需要 RawImage 组件: " + gameObject.name,
                    "RawImageLoader");
                return;
            }

            _textureHandle = ImageLoader.Instance.LoadTexture(bundleName, assetName, priority);
            _textureHandle.Completed += texture =>
            {
                if (currentVersion != _requestVersion || targetRawImage == null)
                {
                    return;
                }

                targetRawImage.texture = texture;
            };
        }

        public void ReleaseCurrent()
        {
            if (_textureHandle != null)
            {
                _textureHandle.Release();
                _textureHandle = null;
            }

            if (clearTextureOnRelease && targetRawImage != null)
            {
                targetRawImage.texture = null;
            }
        }

        private bool EnsureGraphicBinding()
        {
            bool changed = false;
            if (targetRawImage == null)
            {
                RawImage img = GetComponent<RawImage>();
                if (img != null)
                {
                    targetRawImage = img;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
