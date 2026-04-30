using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 音频相关 AB 预热：只加载 AssetBundle 包体（含依赖），不加载具体 AudioClip。
    /// </summary>
    public static class AudioClipPreloader
    {
        /// <summary>战斗默认预热的音频 AB 包名。</summary>
        public static readonly string[] DefaultBattleBundles =
        {
            "audioclips/normalmordel_prefab",
            "audioclips/normalmordel/grid_prefab",
            "audioclips/normalmordel/tower_prefab",
            "audioclips/normalmordel/monster_prefab",
            "audioclips/normalmordel/carrot_prefab",
        };

        /// <summary>
        /// 从 AB 路径（格式 bundle|asset）集合中提取包名去重后预热。
        /// </summary>
        public static void RunBundlesForClipPaths(IReadOnlyList<string> abPaths, Action onComplete = null)
        {
            if (abPaths == null || abPaths.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var bundles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < abPaths.Count; i++)
            {
                string abPath = abPaths[i];
                if (string.IsNullOrEmpty(abPath))
                {
                    continue;
                }

                string normalized = abPath.Trim().Replace('\\', '/');
                int sepIdx = normalized.IndexOf('|');
                if (sepIdx <= 0)
                {
                    continue;
                }

                string bundleName = normalized.Substring(0, sepIdx).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(bundleName))
                {
                    bundles.Add(bundleName);
                }
            }

            RunBundles(bundles, onComplete);
        }

        /// <summary>
        /// 直接按 AB 包名预热（去重）；全部回调结束后触发 <paramref name="onComplete"/>。
        /// </summary>
        public static void RunBundles(IReadOnlyCollection<string> bundleNames, Action onComplete = null)
        {
            if (bundleNames == null || bundleNames.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            AssetBundleManager abm = AssetBundleManager.Instance;
            if (abm == null)
            {
                onComplete?.Invoke();
                return;
            }

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (string b in bundleNames)
            {
                if (!string.IsNullOrEmpty(b))
                {
                    distinct.Add(b);
                }
            }

            if (distinct.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            int total = distinct.Count;
            int finished = 0;
            void DoneOne()
            {
                finished++;
                if (finished >= total)
                {
                    onComplete?.Invoke();
                }
            }

            foreach (string bundle in distinct)
            {
                abm.EnsureBundleLoaded(bundle, _ => DoneOne());
            }
        }

        /// <summary>
        /// 兼容旧接口：传入 AB 路径列表（bundle|asset），实际只预热这些路径涉及的 AB 包，不预加载 AudioClip。
        /// </summary>
        public static void Run(IReadOnlyList<string> abPaths, Action onComplete = null)
        {
            RunBundlesForClipPaths(abPaths, onComplete);
        }

        /// <summary>预热战斗常用音频包。</summary>
        public static void RunBattleDefaults(Action onComplete = null)
        {
            RunBundles(DefaultBattleBundles, onComplete);
        }
    }
}
