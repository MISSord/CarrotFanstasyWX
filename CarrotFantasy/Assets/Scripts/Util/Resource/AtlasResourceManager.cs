using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 图集资源管理（整图集模型，以 AB 包名 BundleName 为唯一键）。
    /// <para>
    /// 流程：Acquire(Sprite) → 图集未就绪则整包 LoadAll → Unload(false) 卸 AB 文件 →
    /// 从内存字典返回 Sprite。同包后续申请直接命中，不再读盘。
    /// </para>
    /// <para>
    /// 引用：由 <see cref="UIImageLoader"/> / <see cref="SpriteLoader"/> 在换图/销毁时
    /// Acquire/Release。整图集 <c>RefCount==0</c> 且非 Resident 时 Destroy 内存中全部 Sprite。
    /// </para>
    /// </summary>
    public sealed class AtlasResourceManager : IResourceDiagnostics
    {
        public const int InvalidToken = -1;

        private const string LogModule = "AtlasResourceManager";

        /// <summary>图集生命周期：未加载 / 整包加载中 / 已在内存（AB 文件已卸）。</summary>
        private enum AtlasLoadState
        {
            None,
            Loading,
            Ready,
        }

        /// <summary>单个图集包的运行时状态与 Sprite 缓存（键 = BundleName）。</summary>
        private sealed class AtlasEntry
        {
            public string BundleName;
            public AtlasLoadState State = AtlasLoadState.None;
            /// <summary>未 Release 的 Acquire 总数。</summary>
            public int RefCount;
            /// <summary>为 true 时引用归零也不清内存（如战斗通用图集）。</summary>
            public bool Resident;
            /// <summary>
            /// 为 true：Sprite 来自运行时 AB，卸图集内存时可 Destroy。
            /// 为 false：Editor 直读工程资源，只能清字典。
            /// </summary>
            public bool DestroySpritesOnUnload;
            public readonly Dictionary<string, Sprite> Sprites =
                new Dictionary<string, Sprite>(StringComparer.Ordinal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            /// <summary>各 Sprite 名持有次数（仅诊断）。</summary>
            public readonly Dictionary<string, int> SpriteRefCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
#endif
            public readonly List<int> WaitingTokens = new List<int>();
        }

        private sealed class TokenInfo
        {
            public int Id;
            public string BundleName;
            public string SpriteName;
            public bool Released;
            public Action<Sprite> Callback;
        }

        private static AtlasResourceManager _instance;
        public static AtlasResourceManager Instance => _instance ?? (_instance = new AtlasResourceManager());

        private readonly Dictionary<string, AtlasEntry> _atlases = new Dictionary<string, AtlasEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, TokenInfo> _tokens = new Dictionary<int, TokenInfo>();
        private readonly ObjectPool<TokenInfo> _tokenPool = new ObjectPool<TokenInfo>(16, 256, ClearToken);

        private int _nextTokenId = 1;
        private bool _defaultsApplied;

        private AtlasResourceManager()
        {
        }

        private static void ClearToken(TokenInfo token)
        {
            token.Id = 0;
            token.BundleName = null;
            token.SpriteName = null;
            token.Released = false;
            token.Callback = null;
        }

        private void RecycleToken(TokenInfo token)
        {
            _tokenPool.Release(token);
        }

        /// <summary>运行时切换常驻；引用归零时 Resident 图集仍留在内存。</summary>
        public void SetResident(string bundleName, bool resident)
        {
            if (!TryNormalizeBundleName(bundleName, out string bundle))
            {
                return;
            }

            GetOrCreateAtlas(bundle).Resident = resident;
        }

        /// <summary>是否为图集 AB（包名含 images_atlas）。</summary>
        public bool IsAtlasBundle(string bundleName)
        {
            return TryNormalizeBundleName(bundleName, out _);
        }

        /// <summary>
        /// 异步取图并增加图集引用。返回 token，须与 <see cref="Release"/> 成对。
        /// <paramref name="bundleName"/> 为图集 AB 包名。
        /// </summary>
        public int AcquireSprite(
            string bundleName,
            string spriteName,
            Action<Sprite> onGot,
            LoadPriority priority = LoadPriority.Medium)
        {
            EnsureDefaults();

            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(spriteName))
            {
                GameLogController.Error("AcquireSprite 失败：bundleName 或 spriteName 为空", LogModule);
                onGot?.Invoke(null);
                return InvalidToken;
            }

            if (!TryNormalizeBundleName(bundleName, out string bundle))
            {
                GameLogController.Error(
                    $"AcquireSprite 失败：不是图集包名 [{bundleName}]",
                    LogModule);
                onGot?.Invoke(null);
                return InvalidToken;
            }

            AtlasEntry atlas = GetOrCreateAtlas(bundle);

            int tokenId = _nextTokenId++;
            TokenInfo token = _tokenPool.Get();
            token.Id = tokenId;
            token.BundleName = bundle;
            token.SpriteName = spriteName;
            token.Released = false;
            token.Callback = onGot;
            _tokens[tokenId] = token;

            atlas.RefCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!atlas.SpriteRefCounts.TryGetValue(spriteName, out int spriteRef))
            {
                spriteRef = 0;
            }

            atlas.SpriteRefCounts[spriteName] = spriteRef + 1;
#endif

            if (atlas.State == AtlasLoadState.Ready)
            {
                ResolveTokenFromAtlas(atlas, token);
                return tokenId;
            }

            atlas.WaitingTokens.Add(tokenId);

            if (atlas.State == AtlasLoadState.Loading)
            {
                return tokenId;
            }

            atlas.State = AtlasLoadState.Loading;
            BeginLoadAtlas(atlas, priority);
            return tokenId;
        }

        /// <summary>与 <see cref="AcquireSprite"/> 成对。</summary>
        public void Release(int tokenId)
        {
            if (tokenId == InvalidToken || !_tokens.TryGetValue(tokenId, out TokenInfo token))
            {
                return;
            }

            if (token.Released)
            {
                return;
            }

            token.Released = true;
            token.Callback = null;
            _tokens.Remove(tokenId);

            string bundleName = token.BundleName;
            string spriteName = token.SpriteName;
            RecycleToken(token);

            if (!_atlases.TryGetValue(bundleName, out AtlasEntry atlas))
            {
                return;
            }

            atlas.WaitingTokens.Remove(tokenId);
            atlas.RefCount = Math.Max(0, atlas.RefCount - 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (atlas.SpriteRefCounts.TryGetValue(spriteName, out int spriteRef))
            {
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

            TryUnloadAtlasMemory(atlas);
        }

        /// <summary>只读查询，不增减引用。图集必须已 Ready。</summary>
        public bool TryPeekSprite(string bundleName, string spriteName, out Sprite sprite)
        {
            sprite = null;
            EnsureDefaults();
            if (!TryNormalizeBundleName(bundleName, out string bundle))
            {
                return false;
            }

            if (!_atlases.TryGetValue(bundle, out AtlasEntry atlas) ||
                atlas.State != AtlasLoadState.Ready)
            {
                return false;
            }

            return atlas.Sprites.TryGetValue(spriteName, out sprite) && sprite != null;
        }

        private void BeginLoadAtlas(AtlasEntry atlas, LoadPriority priority)
        {
            if (AssetBundleManager.Instance == null)
            {
                GameLogController.Error("AssetBundleManager 未初始化", LogModule);
                OnAtlasLoadFinished(atlas, null, destroyOnUnload: false);
                return;
            }

            AssetBundleManager.Instance.LoadAllAssetsAndUnloadFile<Sprite>(
                atlas.BundleName,
                sprites =>
                {
                    bool destroyOnUnload = true;
#if UNITY_EDITOR
                    if (AssetBundleManager.Instance != null &&
                        AssetBundleManager.Instance.IsEditorDirectLoad)
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
                    if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    {
                        continue;
                    }

                    if (!atlas.Sprites.ContainsKey(sprite.name))
                    {
                        atlas.Sprites.Add(sprite.name, sprite);
                    }
                }
            }

            int[] waiting = atlas.WaitingTokens.ToArray();
            atlas.WaitingTokens.Clear();

            if (sprites == null || atlas.Sprites.Count == 0)
            {
                atlas.State = AtlasLoadState.None;
                atlas.DestroySpritesOnUnload = false;
                GameLogController.Error(
                    $"图集加载失败或无 Sprite: {atlas.BundleName}",
                    LogModule);

                for (int i = 0; i < waiting.Length; i++)
                {
                    if (_tokens.TryGetValue(waiting[i], out TokenInfo token) && !token.Released)
                    {
                        InvokeTokenCallback(token, null);
                    }
                }

                return;
            }

            atlas.State = AtlasLoadState.Ready;
            GameLogController.Log(
                $"图集已进内存并卸AB: {atlas.BundleName}, sprites={atlas.Sprites.Count}",
                LogModule);

            for (int i = 0; i < waiting.Length; i++)
            {
                if (_tokens.TryGetValue(waiting[i], out TokenInfo token) && !token.Released)
                {
                    ResolveTokenFromAtlas(atlas, token);
                }
            }
        }

        private void ResolveTokenFromAtlas(AtlasEntry atlas, TokenInfo token)
        {
            Sprite sprite = null;
            if (!atlas.Sprites.TryGetValue(token.SpriteName, out sprite) || sprite == null)
            {
                GameLogController.Error(
                    $"图集 [{atlas.BundleName}] 中不存在 Sprite [{token.SpriteName}]",
                    LogModule);
            }

            InvokeTokenCallback(token, sprite);
        }

        private static void InvokeTokenCallback(TokenInfo token, Sprite sprite)
        {
            Action<Sprite> callback = token.Callback;
            token.Callback = null;
            callback?.Invoke(sprite);
        }

        private void TryUnloadAtlasMemory(AtlasEntry atlas)
        {
            if (atlas.RefCount > 0 || atlas.Resident)
            {
                return;
            }

            if (atlas.State == AtlasLoadState.Loading)
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
            atlas.WaitingTokens.Clear();
            atlas.State = AtlasLoadState.None;
            atlas.DestroySpritesOnUnload = false;
            _atlases.Remove(atlas.BundleName);

            GameLogController.Log($"图集内存已释放: {atlas.BundleName}", LogModule);
        }

        private AtlasEntry GetOrCreateAtlas(string bundleName)
        {
            if (_atlases.TryGetValue(bundleName, out AtlasEntry entry))
            {
                return entry;
            }

            entry = new AtlasEntry
            {
                BundleName = bundleName,
            };
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
            if (!IsAtlasBundleName(key))
            {
                return false;
            }

            normalized = key;
            return true;
        }

        private static bool IsAtlasBundleName(string bundleName)
        {
            return !string.IsNullOrEmpty(bundleName) &&
                   bundleName.IndexOf("images_atlas", StringComparison.OrdinalIgnoreCase) >= 0;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Detail = $"state={atlas.State} sprites={atlas.Sprites.Count} " +
                             $"spriteKeys={atlas.SpriteRefCounts.Count} waiting={atlas.WaitingTokens.Count}",
#else
                    Detail = $"state={atlas.State} sprites={atlas.Sprites.Count} waiting={atlas.WaitingTokens.Count}",
#endif
                });
            }
        }

        public void DumpAliveHandles(string reason = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            List<ResourceUsageSnapshot> list = new List<ResourceUsageSnapshot>();
            CollectSnapshots(list);
            string prefix = string.IsNullOrEmpty(reason) ? "" : "[" + reason + "] ";
            if (list.Count == 0)
            {
                GameLogController.Log(prefix + "Atlas 无缓存条目", LogModule);
                return;
            }

            GameLogController.Log(prefix + $"Atlas 快照 {list.Count} 条（aliveTokens={_tokens.Count}）", LogModule);
            for (int i = 0; i < list.Count; i++)
            {
                ResourceUsageSnapshot s = list[i];
                GameLogController.Log(
                    $"  {s.Key} ref={s.RefCount} cached={s.HasCachedObject} loading={s.IsLoading} resident={s.IsResident} {s.Detail}",
                    LogModule);
            }
#endif
        }

        public void WarnLeakedHandles()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (KeyValuePair<string, AtlasEntry> pair in _atlases)
            {
                if (pair.Value.State == AtlasLoadState.Loading && pair.Value.WaitingTokens.Count > 0)
                {
                    GameLogController.Warning(
                        $"图集长时间 Loading: {pair.Value.BundleName} waiting={pair.Value.WaitingTokens.Count}",
                        LogModule);
                }
            }
#endif
        }

        /// <summary>默认常驻战斗通用图集（包名即键）。</summary>
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
