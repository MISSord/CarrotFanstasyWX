using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CarrotFantasy
{
    public class ResourceLoader
    {
        private static ResourceLoader _resourceLoader;
        public static ResourceLoader Instance
        {
            get
            {
                if (_resourceLoader == null)
                {
                    _resourceLoader = new ResourceLoader();
                }
                return _resourceLoader;
            }
        }

        public GameObject GetGameObject(String path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            // 优先走AB管理器，保持旧接口不变，便于分阶段迁移。
            GameObject abObject = TryLoadGameObjectFromAssetBundle(path);
            if (abObject != null)
            {
                return abObject;
            }

            // 回退到Resources，保证历史路径仍可工作。
            return Resources.Load<GameObject>(path);
        }

        public T loadRes<T>(String path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }

        private GameObject TryLoadGameObjectFromAssetBundle(string path)
        {
            if (AssetBundleManager.Instance == null)
            {
                return null;
            }

            string normalizedPath = path.Replace('\\', '/').Trim('/');
            string assetName = Path.GetFileNameWithoutExtension(normalizedPath);
            if (string.IsNullOrEmpty(assetName))
            {
                return null;
            }

            foreach (string bundleName in BuildBundleNameCandidates(normalizedPath))
            {
                if (string.IsNullOrEmpty(bundleName))
                {
                    continue;
                }

                GameObject result = null;
                AssetBundleManager.Instance.LoadAsset<GameObject>(
                    bundleName,
                    assetName,
                    obj => result = obj,
                    LoadPriority.Sync);

                // Sync优先级只保证AB同步载入，资源对象仍可能在下一些帧完成。
                if (result == null)
                {
                    for (int i = 0; i < 60 && result == null; i++)
                    {
                        AssetBundleManager.Instance.Update();
                    }
                }

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private IEnumerable<string> BuildBundleNameCandidates(string normalizedPath)
        {
            string lowerPath = normalizedPath.ToLowerInvariant();
            string lowerDir = Path.GetDirectoryName(lowerPath)?.Replace('\\', '/');
            string lowerName = Path.GetFileNameWithoutExtension(lowerPath);

            if (!string.IsNullOrEmpty(lowerDir))
            {
                // Prefab主规则：目录名 + "_prefab"
                yield return lowerDir + "_prefab";

                // 图集/资源规则：目录名 + "/" + 文件名
                yield return lowerDir + "/" + lowerName;
            }

            // 兜底：路径自身作为bundle名（兼容特殊命名）
            yield return lowerPath;
        }

    }
}
