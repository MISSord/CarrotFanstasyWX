using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 一份图集精灵引用。Release 归还整图集计数一次。
    /// 图集已在内存时，Load 返回前 IsDone 已经为 true。
    /// </summary>
    public sealed class AtlasHandle : IDisposable
    {
        private static readonly ObjectPool<AtlasHandle> Pool = new ObjectPool<AtlasHandle>(16, 256, Reset);

        internal static AtlasHandle Rent()
        {
            AtlasHandle handle = Pool.Get();
            handle._pooled = false;
            handle._released = false;
            handle._done = false;
            handle._completed = null;
            handle._sprite = null;
            handle.Atlas = null;
            handle.SpriteName = null;
            return handle;
        }

        private static void Reset(AtlasHandle handle)
        {
            handle._completed = null;
            handle._sprite = null;
            handle._done = false;
            handle._released = false;
            handle.Atlas = null;
            handle.SpriteName = null;
            handle._pooled = true;
        }

        internal AtlasEntry Atlas;
        internal string SpriteName;
        internal int LeaseId;

        private Action<Sprite> _completed;
        private Sprite _sprite;
        private bool _done;
        private bool _released;
        private bool _pooled;

        public bool IsValid => !_released && !_pooled;

        public bool IsDone => _done && !_pooled;

        /// <summary>结束时调用一次。订阅时若已经结束，立刻调用。失败时参数为 null。</summary>
        public event Action<Sprite> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (_pooled || _done)
                {
                    value(_pooled || _released ? null : _sprite);
                    return;
                }

                _completed += value;
            }
            remove { _completed -= value; }
        }

        internal void Complete(Sprite sprite)
        {
            if (_done || _released || _pooled)
            {
                return;
            }

            _done = true;
            _sprite = sprite;
            Action<Sprite> callback = _completed;
            _completed = null;
            callback?.Invoke(sprite);
        }

        public void Release()
        {
            if (_released || _pooled)
            {
                return;
            }

            _released = true;
            LeaseId++;
            AtlasLoader.Instance.ReleaseHandle(this);
            Pool.Release(this);
        }

        public void Dispose()
        {
            Release();
        }
    }

    /// <summary>一个图集包的内存记录。AB 文件卸掉后，Sprite 仍留在这里。</summary>
    internal sealed class AtlasEntry
    {
        public string BundleName;
        public AtlasLoadState State = AtlasLoadState.None;
        /// <summary>未 Release 的句柄数量。</summary>
        public int RefCount;
        /// <summary>为 true 时引用归零也不清内存。</summary>
        public bool Resident;
        /// <summary>Sprite 来自运行时 AB 时可 Destroy；编辑器直读工程资源时只能清字典。</summary>
        public bool DestroySpritesOnUnload;
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public readonly Dictionary<string, int> SpriteRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);
#endif
        public readonly List<AtlasHandle> Waiting = new List<AtlasHandle>();
    }

    internal enum AtlasLoadState
    {
        None,
        Loading,
        Ready,
    }

    /// <summary>
    /// 图集加载器。以包名为键整包加载，卸掉 AB 文件后从内存字典取 Sprite。
    /// 和 <see cref="ImageLoader"/> 一样由调用方持有句柄并 Release；不按单张图走资源缓存。
    /// </summary>
    public sealed class AtlasLoader : IResourceDiagnostics
    {
        public static AtlasLoader Instance { get; } = new AtlasLoader();

        private const string LogModule = "AtlasLoader";

        private readonly Dictionary<string, AtlasEntry> _atlases = new Dictionary<string, AtlasEntry>(StringComparer.OrdinalIgnoreCase);
        private bool _defaultsApplied;

        private AtlasLoader()
        {
        }

        /// <summary>运行时切换常驻。常驻图集在引用归零后仍留在内存。</summary>
        public void SetResident(string bundleName, bool resident)
        {
            if (!TryNormalizeBundleName(bundleName, out string bundle))
            {
                return;
            }

            GetOrCreateAtlas(bundle).Resident = resident;
        }

        /// <summary>包名是否包含 images_atlas。</summary>
        public bool IsAtlasBundle(string bundleName)
        {
            return TryNormalizeBundleName(bundleName, out _);
        }

        /// <summary>取一张图并增加图集引用。句柄须 Release。</summary>
        public AtlasHandle Load(string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            EnsureDefaults();
            AtlasHandle handle = AtlasHandle.Rent();
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(spriteName) ||
                !TryNormalizeBundleName(bundleName, out string bundle))
            {
                GameLogController.Error("AtlasLoader.Load 失败，包名或精灵名无效: " + bundleName + " / " + spriteName, LogModule);
                handle.Complete(null);
                return handle;
            }

            AtlasEntry atlas = GetOrCreateAtlas(bundle);
            atlas.RefCount++;
            handle.Atlas = atlas;
            handle.SpriteName = spriteName;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddSpriteRef(atlas, spriteName);
#endif

            if (atlas.State == AtlasLoadState.Ready)
            {
                CompleteFromAtlas(atlas, handle);
                return handle;
            }

            atlas.Waiting.Add(handle);
            if (atlas.State != AtlasLoadState.Loading)
            {
                atlas.State = AtlasLoadState.Loading;
                BeginLoadAtlas(atlas, priority);
            }

            return handle;
        }

        /// <summary>只读查询，不增减引用。图集必须已经加载完成。</summary>
        public bool TryPeekSprite(string bundleName, string spriteName, out Sprite sprite)
        {
            sprite = null;
            EnsureDefaults();
            if (!TryNormalizeBundleName(bundleName, out string bundle))
            {
                return false;
            }

            if (!_atlases.TryGetValue(bundle, out AtlasEntry atlas) || atlas.State != AtlasLoadState.Ready)
            {
                return false;
            }

            return atlas.Sprites.TryGetValue(spriteName, out sprite) && sprite != null;
        }

        internal void ReleaseHandle(AtlasHandle handle)
        {
            AtlasEntry atlas = handle.Atlas;
            string spriteName = handle.SpriteName;
            handle.Atlas = null;
            handle.SpriteName = null;
            if (atlas == null)
            {
                return;
            }

            atlas.Waiting.Remove(handle);
            atlas.RefCount = Math.Max(0, atlas.RefCount - 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RemoveSpriteRef(atlas, spriteName);
#endif
            TryUnloadAtlasMemory(atlas);
        }

        public string DiagnosticsName => "Atlas";

        public void CollectSnapshots(List<ResourceUsageSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            EnsureDefaults();
            foreach (KeyValuePair<string, AtlasEntry> pair in _atlases)
            {
                AtlasEntry atlas = pair.Value;
                into.Add(new ResourceUsageSnapshot
                {
                    Manager = DiagnosticsName,
                    Key = atlas.BundleName,
                    RefCount = atlas.RefCount,
                    HasCachedObject = atlas.State == AtlasLoadState.Ready && atlas.Sprites.Count > 0,
                    IsLoading = atlas.State == AtlasLoadState.Loading,
                    IsResident = atlas.Resident,
                    Detail = "state=" + atlas.State + " sprites=" + atlas.Sprites.Count + " waiting=" + atlas.Waiting.Count,
                });
            }
        }

        public void DumpAliveHandles(string reason = null)
        {
            List<ResourceUsageSnapshot> list = new List<ResourceUsageSnapshot>();
            CollectSnapshots(list);
            string prefix = string.IsNullOrEmpty(reason) ? "" : "[" + reason + "] ";
            if (list.Count == 0)
            {
                GameLogController.Log(prefix + "Atlas 无缓存条目", LogModule);
                return;
            }

            GameLogController.Log(prefix + "Atlas 快照 " + list.Count + " 条", LogModule);
            for (int i = 0; i < list.Count; i++)
            {
                ResourceUsageSnapshot snapshot = list[i];
                GameLogController.Log(
                    "  " + snapshot.Key + " ref=" + snapshot.RefCount +
                    " cached=" + snapshot.HasCachedObject +
                    " loading=" + snapshot.IsLoading +
                    " resident=" + snapshot.IsResident +
                    " " + snapshot.Detail,
                    LogModule);
            }
        }

        public void WarnLeakedHandles()
        {
            foreach (KeyValuePair<string, AtlasEntry> pair in _atlases)
            {
                if (pair.Value.State == AtlasLoadState.Loading && pair.Value.Waiting.Count > 0)
                {
                    GameLogController.Warning(
                        "图集长时间 Loading: " + pair.Value.BundleName + " waiting=" + pair.Value.Waiting.Count,
                        LogModule);
                }
            }
        }

        private void BeginLoadAtlas(AtlasEntry atlas, LoadPriority priority)
        {
            if (AssetBundleManager.Instance == null)
            {
                GameLogController.Error("AssetBundleManager 未初始化", LogModule);
                OnAtlasLoadFinished(atlas, null, false);
                return;
            }

            AssetBundleManager.Instance.LoadAllAssetsAndUnloadFile<Sprite>(
                atlas.BundleName,
                sprites =>
                {
                    bool destroyOnUnload = true;
#if UNITY_EDITOR
                    if (AssetBundleManager.Instance != null && AssetBundleManager.Instance.IsEditorDirectLoad)
                    {
                        destroyOnUnload = false;
                    }
#endif
                    OnAtlasLoadFinished(atlas, sprites, destroyOnUnload);
                },
                priority);
        }

        private void OnAtlasLoadFinished(AtlasEntry atlas, Sprite[] sprites, bool destroyOnUnload)
        {
            if (atlas == null)
            {
                return;
            }

            atlas.Sprites.Clear();
            atlas.DestroySpritesOnUnload = destroyOnUnload;
            if (sprites != null)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite sprite = sprites[i];
                    if (sprite == null || string.IsNullOrEmpty(sprite.name) || atlas.Sprites.ContainsKey(sprite.name))
                    {
                        continue;
                    }

                    atlas.Sprites.Add(sprite.name, sprite);
                }
            }

            AtlasHandle[] waiting = atlas.Waiting.ToArray();
            int[] leases = new int[waiting.Length];
            for (int i = 0; i < waiting.Length; i++)
            {
                leases[i] = waiting[i].LeaseId;
            }

            atlas.Waiting.Clear();
            bool failed = sprites == null || atlas.Sprites.Count == 0;
            if (failed)
            {
                atlas.State = AtlasLoadState.None;
                atlas.DestroySpritesOnUnload = false;
                GameLogController.Error("图集加载失败或无 Sprite: " + atlas.BundleName, LogModule);
            }
            else
            {
                atlas.State = AtlasLoadState.Ready;
                GameLogController.Log("图集已进内存并卸AB: " + atlas.BundleName + ", sprites=" + atlas.Sprites.Count, LogModule);
            }

            for (int i = 0; i < waiting.Length; i++)
            {
                AtlasHandle handle = waiting[i];
                if (handle.LeaseId != leases[i] || !handle.IsValid)
                {
                    continue;
                }

                if (failed)
                {
                    handle.Complete(null);
                }
                else
                {
                    CompleteFromAtlas(atlas, handle);
                }
            }
        }

        private static void CompleteFromAtlas(AtlasEntry atlas, AtlasHandle handle)
        {
            if (!atlas.Sprites.TryGetValue(handle.SpriteName, out Sprite sprite) || sprite == null)
            {
                GameLogController.Error("图集 [" + atlas.BundleName + "] 中不存在 Sprite [" + handle.SpriteName + "]", LogModule);
                handle.Complete(null);
                return;
            }

            handle.Complete(sprite);
        }

        private void TryUnloadAtlasMemory(AtlasEntry atlas)
        {
            if (atlas.RefCount > 0 || atlas.Resident || atlas.State == AtlasLoadState.Loading)
            {
                return;
            }

            if (atlas.DestroySpritesOnUnload)
            {
                foreach (KeyValuePair<string, Sprite> pair in atlas.Sprites)
                {
                    if (pair.Value != null)
                    {
                        UnityEngine.Object.Destroy(pair.Value);
                    }
                }
            }

            atlas.Sprites.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            atlas.SpriteRefCounts.Clear();
#endif
            atlas.Waiting.Clear();
            atlas.State = AtlasLoadState.None;
            atlas.DestroySpritesOnUnload = false;
            _atlases.Remove(atlas.BundleName);
            GameLogController.Log("图集内存已释放: " + atlas.BundleName, LogModule);
        }

        private AtlasEntry GetOrCreateAtlas(string bundleName)
        {
            if (_atlases.TryGetValue(bundleName, out AtlasEntry entry))
            {
                return entry;
            }

            entry = new AtlasEntry { BundleName = bundleName };
            _atlases[bundleName] = entry;
            return entry;
        }

        private static bool TryNormalizeBundleName(string bundleName, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrEmpty(bundleName))
            {
                return false;
            }

            string key = bundleName.Replace('\\', '/');
            if (key.IndexOf("images_atlas", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            normalized = key;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void AddSpriteRef(AtlasEntry atlas, string spriteName)
        {
            if (!atlas.SpriteRefCounts.TryGetValue(spriteName, out int spriteRef))
            {
                spriteRef = 0;
            }

            atlas.SpriteRefCounts[spriteName] = spriteRef + 1;
        }

        private static void RemoveSpriteRef(AtlasEntry atlas, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName) || !atlas.SpriteRefCounts.TryGetValue(spriteName, out int spriteRef))
            {
                return;
            }

            spriteRef = Math.Max(0, spriteRef - 1);
            if (spriteRef <= 0)
            {
                atlas.SpriteRefCounts.Remove(spriteName);
            }
            else
            {
                atlas.SpriteRefCounts[spriteName] = spriteRef;
            }
        }
#endif

        private void EnsureDefaults()
        {
            if (_defaultsApplied)
            {
                return;
            }

            _defaultsApplied = true;
            if (TryNormalizeBundleName(FightViewSpriteAb.CarrotAtlas, out string carrot))
            {
                GetOrCreateAtlas(carrot).Resident = true;
            }

            if (TryNormalizeBundleName(FightViewSpriteAb.NormalMordelAtlas, out string normal))
            {
                GetOrCreateAtlas(normal).Resident = true;
            }
        }
    }
}
