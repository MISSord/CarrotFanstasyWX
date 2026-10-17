using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HybridCLR;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// AOT 侧热更加载：
    /// - Editor：热更程序集已随工程编译，直接取已加载程序集；
    /// - Player：优先读 AB 热更目录中的 DLL，其次 StreamingAssets 基线包。
    /// </summary>
    public static class HybridCLRBootstrap
    {
        public const string HotUpdateAssemblyName = HybridCLRPaths.HotUpdateAssemblyName;
        public const string HotUpdateEntryTypeName = "CarrotFantasy.HotUpdateEntry";

        private static bool s_metadataLoaded;
        private static Assembly s_hotUpdateAssembly;

        public static IHotUpdateEntry CreateEntry()
        {
            EnsureHotUpdateAssemblyLoaded();

            Type entryType = s_hotUpdateAssembly.GetType(HotUpdateEntryTypeName);
            if (entryType == null)
            {
                throw new InvalidOperationException(
                    $"[HybridCLRBootstrap] 程序集中未找到类型 {HotUpdateEntryTypeName}");
            }

            if (!typeof(IHotUpdateEntry).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException(
                    $"[HybridCLRBootstrap] {HotUpdateEntryTypeName} 未实现 IHotUpdateEntry");
            }

            return (IHotUpdateEntry)Activator.CreateInstance(entryType);
        }

        public static void EnsureHotUpdateAssemblyLoaded()
        {
            if (s_hotUpdateAssembly != null)
            {
                return;
            }

            // 1) 程序集里已经存在就直接用。
            //    - Editor：热更程序集随工程编译；
            //    - Player：关闭代码热更后（HybridCLRSettings.enable=false）该程序集会随主包编译进 AOT。
            //    开启热更时它会被 FilterHotFixAssemblies 从主包剔除，这里必然找不到，才会走到第 2 步。
            s_hotUpdateAssembly = FindLoadedAssembly(HotUpdateAssemblyName);
            if (s_hotUpdateAssembly != null)
            {
#if !UNITY_EDITOR
                Debug.Log("[HybridCLRBootstrap] " + HotUpdateAssemblyName
                    + " 已随主包编译进 AOT，跳过热更 DLL 加载（代码热更未生效）。");
#endif
                return;
            }

            // 2) 从 AB 热更目录 / StreamingAssets 基线读 DLL。
            //    只有真的要用热更 DLL 时才需要加载 AOT 补充元数据。
            byte[] hotUpdateBytes = ReadDllBytes(isHotUpdate: true, HotUpdateAssemblyName);
            if (hotUpdateBytes != null && hotUpdateBytes.Length > 0)
            {
                LoadAotMetadataAssemblies();
                s_hotUpdateAssembly = LoadHotUpdateAssembly(hotUpdateBytes);
                Debug.Log($"[HybridCLRBootstrap] 热更程序集已加载: {HotUpdateAssemblyName}, size={hotUpdateBytes.Length}");
                return;
            }

            throw new InvalidOperationException(
                $"[HybridCLRBootstrap] 未能加载热更程序集 {HotUpdateAssemblyName}。" +
                $"既不在主包 AOT 里，也没找到热更 DLL（{HybridCLRPaths.HotUpdateBundleName}）。" +
                "请执行 HybridCLR/Generate/All 与 Tools/HybridCLR/同步 DLL；" +
                "或关闭代码热更（Tools/HybridCLR/关闭代码热更）让该程序集随主包编译。");
        }

        public static void LoadAotMetadataAssemblies(IEnumerable<string> assemblyNames = null)
        {
            if (s_metadataLoaded)
            {
                return;
            }

#if UNITY_EDITOR
            s_metadataLoaded = true;
            return;
#else
            IEnumerable<string> names = assemblyNames ?? HybridCLRPaths.LoadAotMetaAssemblyNames();
            foreach (string name in names)
            {
                byte[] dllBytes = ReadDllBytes(isHotUpdate: false, name);
                if (dllBytes == null || dllBytes.Length == 0)
                {
                    Debug.LogWarning($"[HybridCLRBootstrap] 缺少 AOT 补充元数据: {name}.dll.bytes，已跳过");
                    continue;
                }

                LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
                if (err != LoadImageErrorCode.OK)
                {
                    Debug.LogError($"[HybridCLRBootstrap] LoadMetadataForAOTAssembly 失败: {name}, {err}");
                }
                else
                {
                    Debug.Log($"[HybridCLRBootstrap] AOT 补充元数据已加载: {name}");
                }
            }

            s_metadataLoaded = true;
#endif
        }

        public static Assembly LoadHotUpdateAssembly(byte[] dllBytes)
        {
            if (dllBytes == null || dllBytes.Length == 0)
            {
                throw new ArgumentException("dllBytes 为空", nameof(dllBytes));
            }

            return Assembly.Load(dllBytes);
        }

        /// <summary>
        /// 读取优先级：
        /// 1. AB 热更目录 DownloadedAssetBundles/hybridclr/...
        /// 2. StreamingAssets/HybridCLR/... 基线
        /// 3. persistentDataPath/HybridCLR/...（Android 兼容）
        /// </summary>
        private static byte[] ReadDllBytes(bool isHotUpdate, string assemblyName)
        {
            string bundleName = isHotUpdate
                ? HybridCLRPaths.HotUpdateBundleName
                : HybridCLRPaths.GetAotMetaBundleName(assemblyName);

            string abPath = AssetBundlePathHelper.GetRuntimeLoadPath(bundleName);
            if (!string.IsNullOrEmpty(abPath) && File.Exists(abPath))
            {
                byte[] downloaded = File.ReadAllBytes(abPath);
                if (!IsPersistedHotUpdateFile(abPath) || MatchesSignedManifest(bundleName, downloaded))
                {
                    return downloaded;
                }

                Debug.LogError("[HybridCLRBootstrap] 热更文件与已签名清单不一致，拒绝加载并降级到基线: " + bundleName);

                // 必须把这份坏缓存删掉：否则下次启动仍会命中它、继续被拒，
                // 客户端会长期静默地跑基线旧代码（表现为黑屏 / UI 为空，且日志只在首次出现）。
                // 删除后更新检查会认为该包缺失并重新下载，从而自愈。
                TryDeleteCorruptFile(abPath);
            }

            string streamingRelative = HybridCLRPaths.GetStreamingBaselineRelativePath(isHotUpdate, assemblyName);
            string streamingPath = Path.Combine(Application.streamingAssetsPath, streamingRelative);
            if (File.Exists(streamingPath))
            {
                return File.ReadAllBytes(streamingPath);
            }

            string persistentHybrid = Path.Combine(Application.persistentDataPath, streamingRelative);
            if (File.Exists(persistentHybrid))
            {
                byte[] persisted = File.ReadAllBytes(persistentHybrid);
                if (!isHotUpdate || MatchesSignedManifest(bundleName, persisted))
                {
                    return persisted;
                }

                Debug.LogError("[HybridCLRBootstrap] 持久化热更文件与已签名清单不一致，拒绝加载: " + bundleName);
            }

            return null;
        }

        /// <summary>删除校验失败的缓存文件，让更新流程下次重新下载它。</summary>
        private static void TryDeleteCorruptFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log("[HybridCLRBootstrap] 已删除校验失败的缓存文件: " + path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HybridCLRBootstrap] 删除校验失败的缓存文件时出错: " + path + " : " + e.Message);
            }
        }

        private static bool IsPersistedHotUpdateFile(string filePath)
        {
            string root = AssetBundlePathHelper.GetPersistentDownloadDirectory();
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string fullFile = Path.GetFullPath(filePath);
            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>持久化目录里的热更 DLL 必须对上已注入清单的 Hash。清单本身须先通过签名。</summary>
        private static bool MatchesSignedManifest(string bundleName, byte[] dllBytes)
        {
            if (AssetBundleManager.Instance == null
                || !AssetBundleManager.Instance.TryGetBundleHash(bundleName, out string expectedHash))
            {
                Debug.LogError("[HybridCLRBootstrap] 没有已签名清单中的 Hash: " + bundleName);
                return false;
            }

            string actual = ComputeMd5(dllBytes);
            return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeMd5(byte[] data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            return null;
        }
    }
}
