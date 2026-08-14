using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// Prefab 模板加载（有感）：业务必须 <see cref="Load"/> / <see cref="Unload"/> 成对调用。
    /// 不负责 Instantiate；同 bundle+asset 共享底层模板，按 Load 次数引用计数。
    /// 用于面板 ViewLoader、战斗预加载、长驻 Tip 等；临时实例请用 <see cref="GameObjectResourceManager"/>。
    /// </summary>
    public sealed class PrefabResourceManager : IResourceDiagnostics
    {
        public const int InvalidHandle = -1;

        private const string LogModule = "PrefabResourceManager";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float LeakWarnSeconds = 120f;
#endif

        private sealed class TemplateSlot
        {
            public GameObject Template;
            public int RefCount;
            public AssetLoadHandle LoadHandle = AssetLoadHandle.Invalid;
            public bool IsLoading;
            public readonly List<int> WaitingHandles = new List<int>();
        }

        private sealed class HandleInfo
        {
            public int Id;
            public string BundleName;
            public string AssetName;
            public bool Released;
            public Action<GameObject> Callback;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public float LoadRealtime;
            public string StackTrace;
#endif
        }

        private static PrefabResourceManager _instance;
        public static PrefabResourceManager Instance => _instance ?? (_instance = new PrefabResourceManager());

        private readonly Dictionary<string, TemplateSlot> _slots =
            new Dictionary<string, TemplateSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HandleInfo> _handles = new Dictionary<int, HandleInfo>();
        private readonly ObjectPool<HandleInfo> _handlePool = new ObjectPool<HandleInfo>(
            16,
            256,
            ClearHandle);

        private int _nextHandleId = 1;

        private PrefabResourceManager()
        {
        }

        private static void ClearHandle(HandleInfo info)
        {
            info.Id = 0;
            info.BundleName = null;
            info.AssetName = null;
            info.Released = false;
            info.Callback = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            info.LoadRealtime = 0f;
            info.StackTrace = null;
#endif
        }

        private void RecycleHandle(HandleInfo info)
        {
            _handlePool.Release(info);
        }

        public static string MakeKey(string bundleName, string assetName)
        {
            return bundleName + "|" + assetName;
        }

        /// <summary>
        /// 加载模板。每次成功发起的 Load 都必须对应一次 <see cref="Unload"/>。
        /// </summary>
        public int Load(
            string bundleName,
            string assetName,
            Action<GameObject> onLoaded,
            LoadPriority priority = LoadPriority.Medium)
        {
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                GameLogController.Error("Load 失败：bundleName 或 assetName 为空", LogModule);
                onLoaded?.Invoke(null);
                return InvalidHandle;
            }

            int handleId = _nextHandleId++;
            HandleInfo info = _handlePool.Get();
            info.Id = handleId;
            info.BundleName = bundleName;
            info.AssetName = assetName;
            info.Released = false;
            info.Callback = onLoaded;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            info.LoadRealtime = Time.realtimeSinceStartup;
            info.StackTrace = new StackTrace(1, true).ToString();
#endif
            _handles[handleId] = info;

            string key = MakeKey(bundleName, assetName);
            if (!_slots.TryGetValue(key, out TemplateSlot slot))
            {
                slot = new TemplateSlot();
                _slots[key] = slot;
            }

            slot.RefCount++;

            if (slot.Template != null)
            {
                InvokeCallback(info, slot.Template);
                return handleId;
            }

            slot.WaitingHandles.Add(handleId);

            if (slot.IsLoading)
            {
                return handleId;
            }

            slot.IsLoading = true;
            slot.LoadHandle = AssetLoadManager.Instance.LoadAsset<GameObject>(
                bundleName,
                assetName,
                go => OnTemplateLoaded(key, go),
                priority,
                "PrefabResourceManager.Load");

            if (!slot.LoadHandle.IsValid && slot.IsLoading)
            {
                OnTemplateLoaded(key, null);
            }

            return handleId;
        }

        /// <summary>与 <see cref="Load"/> 成对调用。</summary>
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
            _handles.Remove(handleId);

            string key = MakeKey(info.BundleName, info.AssetName);
            RecycleHandle(info);

            if (!_slots.TryGetValue(key, out TemplateSlot slot))
            {
                return;
            }

            slot.WaitingHandles.Remove(handleId);
            slot.RefCount = Math.Max(0, slot.RefCount - 1);

            if (slot.RefCount <= 0)
            {
                if (slot.LoadHandle.IsValid)
                {
                    slot.LoadHandle.Dispose();
                    slot.LoadHandle = AssetLoadHandle.Invalid;
                }

                slot.Template = null;
                slot.IsLoading = false;
                slot.WaitingHandles.Clear();
                _slots.Remove(key);
            }
        }

        /// <summary>仅在仍有未 Unload 的 Load 时可用；不能代替持有。</summary>
        public bool TryGetLoaded(string bundleName, string assetName, out GameObject template)
        {
            template = null;
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            if (!_slots.TryGetValue(MakeKey(bundleName, assetName), out TemplateSlot slot) ||
                slot.Template == null)
            {
                return false;
            }

            template = slot.Template;
            return true;
        }

        /// <summary>调试：未 Unload 的 handle 数量。</summary>
        public int AliveHandleCount => _handles.Count;

        public string DiagnosticsName => "Prefab";

        public void CollectSnapshots(List<ResourceUsageSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            foreach (KeyValuePair<string, TemplateSlot> pair in _slots)
            {
                TemplateSlot slot = pair.Value;
                into.Add(new ResourceUsageSnapshot
                {
                    Manager = DiagnosticsName,
                    Key = pair.Key,
                    RefCount = slot.RefCount,
                    HasCachedObject = slot.Template != null,
                    IsLoading = slot.IsLoading,
                    IsResident = false,
                    Detail = $"waiting={slot.WaitingHandles.Count}",
                });
            }
        }

        /// <summary>调试：打印未配对 Unload 的 Load（Editor/Development）。</summary>
        public void DumpAliveHandles(string reason = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_handles.Count == 0)
            {
                GameLogController.Log(
                    string.IsNullOrEmpty(reason)
                        ? "无未释放 Prefab handle"
                        : $"[{reason}] 无未释放 Prefab handle",
                    LogModule);
                return;
            }

            float now = Time.realtimeSinceStartup;
            GameLogController.Warning(
                $"{(string.IsNullOrEmpty(reason) ? "" : "[" + reason + "] ")}未释放 Prefab handle 共 {_handles.Count} 个：",
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

        /// <summary>调试：对存活超过阈值的 handle 告警。</summary>
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

        private void OnTemplateLoaded(string key, GameObject template)
        {
            if (!_slots.TryGetValue(key, out TemplateSlot slot))
            {
                return;
            }

            slot.IsLoading = false;
            slot.Template = template;

            if (slot.WaitingHandles.Count == 0)
            {
                return;
            }

            int[] waiting = slot.WaitingHandles.ToArray();
            slot.WaitingHandles.Clear();

            for (int i = 0; i < waiting.Length; i++)
            {
                if (_handles.TryGetValue(waiting[i], out HandleInfo info) && !info.Released)
                {
                    InvokeCallback(info, template);
                }
            }
        }

        private static void InvokeCallback(HandleInfo info, GameObject template)
        {
            Action<GameObject> callback = info.Callback;
            info.Callback = null;
            callback?.Invoke(template);
        }
    }
}
