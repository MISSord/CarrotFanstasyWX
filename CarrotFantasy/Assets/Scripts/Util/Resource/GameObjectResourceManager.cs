using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 通过 <see cref="PrefabResourceManager"/> 加载模板并返回 Instantiate 后的实例（有感）。
    /// 业务必须 <see cref="Load"/> / <see cref="Unload"/> 成对：Unload 时销毁实例并 Unload 模板引用。
    /// 适合特效/临时物；面板与长驻模板请直接用 <see cref="PrefabResourceManager"/>。
    /// </summary>
    public sealed class GameObjectResourceManager : IResourceDiagnostics
    {
        public const int InvalidHandle = -1;

        private const string LogModule = "GameObjectResourceManager";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float LeakWarnSeconds = 120f;
#endif

        private sealed class HandleInfo
        {
            public int Id;
            public string BundleName;
            public string AssetName;
            public int PrefabHandle = PrefabResourceManager.InvalidHandle;
            public GameObject Instance;
            public bool Released;
            public Action<GameObject> Callback;
            public Transform Parent;
            public bool WorldPositionStays;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public float LoadRealtime;
            public string StackTrace;
#endif
        }

        private static GameObjectResourceManager _instance;
        public static GameObjectResourceManager Instance => _instance ?? (_instance = new GameObjectResourceManager());

        private readonly Dictionary<int, HandleInfo> _handles = new Dictionary<int, HandleInfo>();
        private readonly ObjectPool<HandleInfo> _handlePool = new ObjectPool<HandleInfo>(
            16,
            256,
            ClearHandle);

        private int _nextHandleId = 1;

        private GameObjectResourceManager()
        {
        }

        private static void ClearHandle(HandleInfo info)
        {
            info.Id = 0;
            info.BundleName = null;
            info.AssetName = null;
            info.PrefabHandle = PrefabResourceManager.InvalidHandle;
            info.Instance = null;
            info.Released = false;
            info.Callback = null;
            info.Parent = null;
            info.WorldPositionStays = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            info.LoadRealtime = 0f;
            info.StackTrace = null;
#endif
        }

        private void RecycleHandle(HandleInfo info)
        {
            _handlePool.Release(info);
        }

        /// <summary>
        /// 异步实例化。每次 Load 必须对应一次 <see cref="Unload"/>。
        /// </summary>
        public int Load(
            string bundleName,
            string assetName,
            Action<GameObject> onReady,
            Transform parent = null,
            bool worldPositionStays = false,
            LoadPriority priority = LoadPriority.Medium)
        {
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                GameLogController.Error("Load 失败：bundleName 或 assetName 为空", LogModule);
                onReady?.Invoke(null);
                return InvalidHandle;
            }

            int handleId = _nextHandleId++;
            HandleInfo info = _handlePool.Get();
            info.Id = handleId;
            info.BundleName = bundleName;
            info.AssetName = assetName;
            info.PrefabHandle = PrefabResourceManager.InvalidHandle;
            info.Instance = null;
            info.Released = false;
            info.Callback = onReady;
            info.Parent = parent;
            info.WorldPositionStays = worldPositionStays;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            info.LoadRealtime = Time.realtimeSinceStartup;
            info.StackTrace = new StackTrace(1, true).ToString();
#endif
            _handles[handleId] = info;

            info.PrefabHandle = PrefabResourceManager.Instance.Load(
                bundleName,
                assetName,
                template => OnTemplateLoaded(handleId, template),
                priority);

            if (info.PrefabHandle == PrefabResourceManager.InvalidHandle && !info.Released)
            {
                // PrefabRM 同步失败时仍会进回调；此处仅兜底
                if (_handles.ContainsKey(handleId))
                {
                    OnTemplateLoaded(handleId, null);
                }
            }

            return handleId;
        }

        /// <summary>与 <see cref="Load"/> 成对：销毁实例并释放模板引用。</summary>
        public void Unload(int handleId)
        {
            if (handleId == InvalidHandle || !_handles.TryGetValue(handleId, out HandleInfo info))
            {
                return;
            }

            if (info.Released)
            {
                return;
            }

            info.Released = true;
            info.Callback = null;

            if (info.Instance != null)
            {
                UnityEngine.Object.Destroy(info.Instance);
                info.Instance = null;
            }

            if (info.PrefabHandle != PrefabResourceManager.InvalidHandle)
            {
                PrefabResourceManager.Instance.Unload(info.PrefabHandle);
                info.PrefabHandle = PrefabResourceManager.InvalidHandle;
            }

            _handles.Remove(handleId);
            RecycleHandle(info);
        }

        public int AliveHandleCount => _handles.Count;

        public string DiagnosticsName => "GameObject";

        public void CollectSnapshots(List<ResourceUsageSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            foreach (KeyValuePair<int, HandleInfo> pair in _handles)
            {
                HandleInfo info = pair.Value;
                into.Add(new ResourceUsageSnapshot
                {
                    Manager = DiagnosticsName,
                    Key = PrefabResourceManager.MakeKey(info.BundleName, info.AssetName),
                    RefCount = 1,
                    HasCachedObject = info.Instance != null,
                    IsLoading = info.Instance == null && !info.Released,
                    IsResident = false,
                    Detail = $"handle={info.Id} instanceAlive={(info.Instance != null)}",
                });
            }
        }

        public void DumpAliveHandles(string reason = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_handles.Count == 0)
            {
                GameLogController.Log(
                    string.IsNullOrEmpty(reason)
                        ? "无未释放 GameObject handle"
                        : $"[{reason}] 无未释放 GameObject handle",
                    LogModule);
                return;
            }

            float now = Time.realtimeSinceStartup;
            GameLogController.Warning(
                $"{(string.IsNullOrEmpty(reason) ? "" : "[" + reason + "] ")}未释放 GameObject handle 共 {_handles.Count} 个：",
                LogModule);

            foreach (KeyValuePair<int, HandleInfo> pair in _handles)
            {
                HandleInfo info = pair.Value;
                float age = now - info.LoadRealtime;
                GameLogController.Warning(
                    $"  handle={info.Id} age={age:F1}s {info.BundleName}/{info.AssetName}\n{info.StackTrace}",
                    LogModule);
            }
#else
            GameLogController.Log("DumpAliveHandles 仅 Editor/Development 可用", LogModule);
#endif
        }

        public void WarnLeakedHandles()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_handles.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<int, HandleInfo> pair in _handles)
            {
                HandleInfo info = pair.Value;
                float age = now - info.LoadRealtime;
                if (age < LeakWarnSeconds)
                {
                    continue;
                }

                GameLogController.Warning(
                    $"疑似漏 Unload: handle={info.Id} age={age:F0}s {info.BundleName}/{info.AssetName}",
                    LogModule);
            }
#endif
        }

        private void OnTemplateLoaded(int handleId, GameObject template)
        {
            if (!_handles.TryGetValue(handleId, out HandleInfo info) || info.Released)
            {
                return;
            }

            if (template == null)
            {
                Action<GameObject> failCallback = info.Callback;
                info.Callback = null;
                failCallback?.Invoke(null);
                return;
            }

            info.Instance = CreateInstance(template, info.Parent, info.WorldPositionStays);
            Action<GameObject> callback = info.Callback;
            info.Callback = null;
            callback?.Invoke(info.Instance);
        }

        private static GameObject CreateInstance(GameObject template, Transform parent, bool worldPositionStays)
        {
            if (parent != null)
            {
                return UnityEngine.Object.Instantiate(template, parent, worldPositionStays);
            }

            return UnityEngine.Object.Instantiate(template);
        }
    }
}
