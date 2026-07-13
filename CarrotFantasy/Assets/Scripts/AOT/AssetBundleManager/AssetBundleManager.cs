using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

// 加载优先级枚举
#if UNITY_EDITOR
public enum LoadMode
{
    /// <summary>
    /// 开发模式
    /// </summary>
    Development = 0,
    /// <summary>
    /// 生产模式
    /// </summary>
    Production = 1,
    /// <summary>
    /// 测试模式
    /// </summary>
    Testing = 2,
    /// <summary>
    /// 演示模式
    /// </summary>
    Demo = 3,
    /// <summary>
    /// 调试模式
    /// </summary>
    DebugMode = 4
}
#endif

public enum LoadPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Sync = 3,
    Max = 4
}

// 资源加载回调
public delegate void AssetLoadCallback<T>(T asset) where T : UnityEngine.Object;
public delegate void AssetLoadCallback(UnityEngine.Object asset);

// 资源项信息
public class AssetItem
{
    public string bundleName;
    public string assetName;
    public UnityEngine.Object assetObject;
    public Type expectedType;
    public int referenceCount;
    public AssetBundleRequest loadRequest;
    public DateTime lastUseTime;
    private List<AssetLoadCallback> _callbacks;

    //回调ID管理，用于精确移除特定回调
    private Dictionary<int, AssetLoadCallback> _callbackWithIds;
    private int _callbackIdCounter = 0;

    public AssetItem(string bundle, string asset)
    {
        bundleName = bundle;
        assetName = asset;
        referenceCount = 0;
        lastUseTime = DateTime.Now;
        _callbacks = new List<AssetLoadCallback>();
        _callbackWithIds = new Dictionary<int, AssetLoadCallback>();
    }

    public void AddReference()
    {
        referenceCount++;
        lastUseTime = DateTime.Now;
    }

    public void RemoveReference()
    {
        referenceCount = Mathf.Max(0, referenceCount - 1);
        lastUseTime = DateTime.Now;
    }

    public int AddCallback(AssetLoadCallback callback)
    {
        if (callback != null)
        {
            _callbacks.Add(callback);

            // 同时添加到ID管理字典
            int callbackId = _callbackIdCounter++;
            _callbackWithIds[callbackId] = callback;
            return callbackId;
        }
        return -1;
    }

    public void RemoveCallback(int callbackId)
    {
        if (_callbackWithIds.TryGetValue(callbackId, out AssetLoadCallback callback))
        {
            _callbacks.Remove(callback);
            _callbackWithIds.Remove(callbackId);
        }
    }

    // 新增：通过回调委托实例移除特定回调
    public void RemoveCallback(AssetLoadCallback callback)
    {
        if (callback != null)
        {
            _callbacks.Remove(callback);

            // 从ID管理字典中移除
            var itemsToRemove = _callbackWithIds.Where(kvp => kvp.Value == callback).ToList();
            foreach (var item in itemsToRemove)
            {
                _callbackWithIds.Remove(item.Key);
            }
        }
    }

    public void RemoveAllCallbacks()
    {
        _callbacks.Clear();
        _callbackWithIds.Clear();
    }

    public void ExecuteCallbacks()
    {
        foreach (var callback in _callbacks)
        {
            try
            {
                callback?.Invoke(assetObject);
            }
            catch (Exception e)
            {
                GameLogController.Error($"执行资源回调失败: {assetName}, 错误: {e.Message}", "AssetBundleManager");
            }
        }

        _callbacks.Clear();
        _callbackWithIds.Clear();
    }

    // 新增：获取回调数量
    public int GetCallbackCount()
    {
        return _callbacks.Count;
    }
}

// AB包信息
public class BundleInfo
{
    public string bundleName;
    public AssetBundle bundle;
    public int referenceCount; //这里要保存全部AssetItem的referenceCount数量之和
    public DateTime lastUseTime;
    public DateTime loadTime;
    public bool isLoading;
    public bool isLoaded;
    public Dictionary<string, AssetItem> assetItems;

    // 加载相关
    public LoadPriority loadPriority;
    public AssetBundleCreateRequest bundleRequest;
    public List<string> pendingAssets; // 等待加载的资源列表

    // 新增依赖相关字段
    public List<string> Dependencies; // 依赖的AB包列表
    public int loadedDependenciesCount; // 已加载的依赖数量
    public bool areDependenciesLoaded; // 所有依赖是否已加载
    public Action onDependenciesLoaded; // 依赖加载完成的回调
    private HashSet<string> _referencedDependencies; // 已计入引用计数的依赖集合（按父包去重）
    private HashSet<string> _loadedDependencySet; // 已完成加载回调的依赖集合（防重复）

    public BundleInfo(string name)
    {
        bundleName = name;
        referenceCount = 0;
        lastUseTime = DateTime.Now;
        loadTime = DateTime.Now;
        assetItems = new Dictionary<string, AssetItem>();
        isLoading = false;
        isLoaded = false;
        pendingAssets = new List<string>();

        // 初始化新增字段
        Dependencies = new List<string>();
        loadedDependenciesCount = 0;
        areDependenciesLoaded = false;
        _referencedDependencies = new HashSet<string>();
        _loadedDependencySet = new HashSet<string>();
    }

    public void AddReference()
    {
        referenceCount++;
        lastUseTime = DateTime.Now;
    }

    public void RemoveReference()
    {
        referenceCount = Mathf.Max(0, referenceCount - 1);
        lastUseTime = DateTime.Now;
    }

    public AssetItem GetOrCreateAssetItem(string assetName)
    {
        if (!assetItems.TryGetValue(assetName, out AssetItem item))
        {
            item = new AssetItem(bundleName, assetName);
            assetItems[assetName] = item;
        }
        return item;
    }

    public AssetItem GetAssetItem(string assetName)
    {
        assetItems.TryGetValue(assetName, out AssetItem item);
        return item;
    }

    public void AddPendingAsset(string assetName)
    {
        if (!pendingAssets.Contains(assetName))
        {
            pendingAssets.Add(assetName);
            GameLogController.Log($"{assetName}加入{bundleName}待加载队列", "AssetBundleManager");
        }
    }

    public void RemovePendingAsset(string assetName)
    {
        GameLogController.Log($"{assetName}从{bundleName}待加载队列移除", "AssetBundleManager");
        pendingAssets.Remove(assetName);
    }

    public void Unload(bool unloadAllLoadedObjects = false)
    {
        if (bundle != null)
        {
            bundle.Unload(unloadAllLoadedObjects);
            bundle = null;
        }

        // 清理所有资源的回调
        foreach (var assetItem in assetItems.Values)
        {
            assetItem.RemoveAllCallbacks();
        }
        assetItems.Clear();
        pendingAssets.Clear();
        isLoaded = false;
        isLoading = false;
        _referencedDependencies.Clear();
        _loadedDependencySet.Clear();
    }

    // 新增方法：检查依赖是否全部加载完成
    public void CheckDependenciesLoaded()
    {
        if (Dependencies.Count > 0 && loadedDependenciesCount >= Dependencies.Count)
        {
            areDependenciesLoaded = true;
            onDependenciesLoaded?.Invoke();
            onDependenciesLoaded = null; // 执行后清空回调
        }
        else if (Dependencies.Count == 0)
        {
            areDependenciesLoaded = true;
            onDependenciesLoaded?.Invoke();
            onDependenciesLoaded = null;
        }
    }

    // 新增方法：依赖加载完成回调
    public void OnDependencyLoaded(string dependencyName)
    {
        if (Dependencies.Contains(dependencyName) && _loadedDependencySet.Add(dependencyName))
        {
            loadedDependenciesCount++;
            CheckDependenciesLoaded();
        }
    }

    public void ResetDependencyLoadState()
    {
        loadedDependenciesCount = 0;
        areDependenciesLoaded = false;
        _loadedDependencySet.Clear();
    }

    public bool TryMarkDependencyReferenced(string dependencyName)
    {
        return _referencedDependencies.Add(dependencyName);
    }

    public bool HasReferencedDependency(string dependencyName)
    {
        return _referencedDependencies.Contains(dependencyName);
    }
}

#if UNITY_EDITOR
public struct AssetBaseLoadInfo
{
    public string bundleName;
    public string assetName;
    public System.Type expectedType;
    public AssetLoadCallback _callback;
}
#endif

// AB包加载管理器
public class AssetBundleManager
{
    private static AssetBundleManager _instance;
    public static AssetBundleManager Instance
    {
        get
        {
            return _instance;
        }
    }

    public AssetBundleManager()
    {
        _instance = this;
    }

    // AB包清单数据
    private Dictionary<string, CustomAssetBundleInfo> _bundleManifests = new Dictionary<string, CustomAssetBundleInfo>(System.StringComparer.OrdinalIgnoreCase);
    private CustomManifest _currentManifest;

    // 已加载的AB包
    private Dictionary<string, BundleInfo> _loadedBundles = new Dictionary<string, BundleInfo>(System.StringComparer.OrdinalIgnoreCase);

    //全部的AB包信息
    private Dictionary<string, BundleInfo> _allBundleDic = new Dictionary<string, BundleInfo>(System.StringComparer.OrdinalIgnoreCase);

    // 不同优先级的加载队列
    private Queue<BundleInfo> _lowPriorityQueue = new Queue<BundleInfo>();
    private Queue<BundleInfo> _mediumPriorityQueue = new Queue<BundleInfo>();
    private Queue<BundleInfo> _highPriorityQueue = new Queue<BundleInfo>();

    // 正在加载的AB包
    private List<BundleInfo> _loadingBundles = new List<BundleInfo>();
    private HashSet<string> _loadingBundleNames = new HashSet<string>();

    // 资源加载等待队列
    private Queue<AssetItem> _assetLoadingQueue = new Queue<AssetItem>();
    private HashSet<string> _queuedAssetKeys = new HashSet<string>();

    //正在加载的资源
    private List<AssetItem> _loadingAssets = new List<AssetItem>();
    private HashSet<string> _loadingAssetKeys = new HashSet<string>();
    private HashSet<string> _cancelledAssetKeys = new HashSet<string>();

    public int maxConcurrentLoads = 3;
    public int maxConcurrentAssetLoads = 5; // 最大同时加载资源数量
    public float unloadCheckInterval = 60f;
    public int unloadReferenceThreshold = 60;

    // 事件
    public System.Action<string> OnBundleLoaded;
    public System.Action<string> OnBundleLoadFailed;

#if UNITY_EDITOR
    private LoadMode loadMode;
    private List<AssetBaseLoadInfo> assetBaseLoadInfos = new List<AssetBaseLoadInfo>();
#endif

    public void Init()
    {
        SRPScheduler.Instance.StartCoroutine(UnloadCheckCoroutine());

#if UNITY_EDITOR
        loadMode = (LoadMode)EditorPrefs.GetInt("GameLoadMode", 0);
#endif
    }

    /// <summary>卸载内存中的 AB 包并清空清单（清除缓存时调用）。</summary>
    public void ClearRuntimeCache()
    {
        DeleteMe();
        _bundleManifests.Clear();
        _currentManifest = null;
    }

    public void DeleteMe()
    {
        //清除数据
        foreach (var bundleInfo in _allBundleDic.Values)
        {
            bundleInfo.Unload(true);
        }
        _allBundleDic.Clear();

        _lowPriorityQueue.Clear();
        _mediumPriorityQueue.Clear();
        _highPriorityQueue.Clear();

        // 清理资源加载相关
        _loadingBundles.Clear();
        _loadingBundleNames.Clear();
        _assetLoadingQueue.Clear();
        _queuedAssetKeys.Clear();
        _loadingAssets.Clear();
        _loadingAssetKeys.Clear();
        _cancelledAssetKeys.Clear();
        _loadedBundles.Clear();
    }

    public void Update()
    {
        UpdateBundleLoading();
        UpdateAssetLoading();
#if UNITY_EDITOR
        UpdateAssetBaseLoad();
#endif
        AssetBundleDownloader.Instance?.Update();
    }

    #region 公共接口

    // 设置AB包清单
    public void SetAssetBundleItem(CustomManifest manifests)
    {
        if (manifests?.AssetBundles == null)
        {
            GameLogController.Warning("SetAssetBundleItem: manifest 为空", "AssetBundleManager");
            return;
        }

        _currentManifest = manifests;
        _bundleManifests.Clear();
        foreach (var abInfo in manifests.AssetBundles)
        {
            _bundleManifests[abInfo.BundleName] = abInfo;
        }
        GameLogController.Log($"AB包清单设置完成，共 {manifests.AssetBundles.Count} 个AB包信息", "AssetBundleManager");
    }

    public bool HasManifest => _bundleManifests.Count > 0;

    /// <summary>
    /// 泛型版本
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bundleName"></param>
    /// <param name="assetName"></param>
    /// <param name="callback"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    public int LoadAsset<T>(string bundleName, string assetName, AssetLoadCallback<T> callback = null, LoadPriority priority = LoadPriority.Medium) where T : UnityEngine.Object
    {
        AssetLoadCallback genericCallback = null;
        if (callback != null)
        {
            genericCallback = (asset) =>
            {
                T typedAsset = asset as T;
                if (typedAsset == null && asset != null)
                {
                    GameLogController.Error($"资源类型不匹配: {assetName}, 期望: {typeof(T).Name}, 实际: {asset.GetType().Name}", "AssetBundleManager");
                }
                callback?.Invoke(typedAsset);
            };
        }

#if UNITY_EDITOR
        if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
        {
            AssetBaseLoadInfo info = new AssetBaseLoadInfo()
            {
                bundleName = bundleName,
                assetName = assetName,
                expectedType = typeof(T),
                _callback = genericCallback,
            };
            assetBaseLoadInfos.Add(info);
            return -1;
        }
#endif

        return LoadAsset(bundleName, assetName, genericCallback, priority, typeof(T));
    }

    /// <summary>
    /// 仅加载 AB 包（含依赖），不加载包内具体资源；用于预热磁盘/解压，后续 <see cref="LoadAsset{T}"/> 会更快。
    /// 编辑器 Development / Debug 模式下不加载真实 AB，直接视为成功。
    /// </summary>
    /// <param name="bundleName">清单中的包名（如 audioclips/normalmordel_prefab）。</param>
    /// <param name="onComplete">是否成功（清单缺失、加载失败为 false）；成功时会保留一次引用计数以免立即被卸载。</param>
    /// <param name="priority">包加载优先级。</param>
    public void EnsureBundleLoaded(string bundleName, Action<bool> onComplete = null, LoadPriority priority = LoadPriority.Medium)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            GameLogController.Error("EnsureBundleLoaded: bundleName 为空", "AssetBundleManager");
            onComplete?.Invoke(false);
            return;
        }

#if UNITY_EDITOR
        if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
        {
            onComplete?.Invoke(true);
            return;
        }
#endif

        if (!_bundleManifests.ContainsKey(bundleName))
        {
            GameLogController.Warning($"EnsureBundleLoaded: 清单中不存在包 {bundleName}", "AssetBundleManager");
            onComplete?.Invoke(false);
            return;
        }

        BundleInfo already = GetBundleInfo(bundleName);
        if (already != null && already.isLoaded)
        {
            onComplete?.Invoke(true);
            return;
        }

        BundleInfo bundleInfo = GetOrCreateBundleInfo(bundleName);
        bundleInfo.AddReference();

        already = GetBundleInfo(bundleName);
        if (already != null && already.isLoaded)
        {
            onComplete?.Invoke(true);
            return;
        }

        bool completed = false;

        Action<string> onLoaded = null;
        Action<string> onFailed = null;

        void Complete(bool ok)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            OnBundleLoaded -= onLoaded;
            OnBundleLoadFailed -= onFailed;
            if (!ok)
            {
                bundleInfo.RemoveReference();
            }

            onComplete?.Invoke(ok);
        }

        onLoaded = (string name) =>
        {
            if (name == bundleName)
            {
                Complete(true);
            }
        };
        onFailed = (string name) =>
        {
            if (name == bundleName)
            {
                Complete(false);
            }
        };

        OnBundleLoaded += onLoaded;
        OnBundleLoadFailed += onFailed;

        LoadBundleWithDependencies(bundleName, priority);

        already = GetBundleInfo(bundleName);
        if (already != null && already.isLoaded)
        {
            Complete(true);
        }
    }

#if UNITY_EDITOR
    private void UpdateAssetBaseLoad()
    {
        if (assetBaseLoadInfos.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < assetBaseLoadInfos.Count; ++i)
        {
            AssetBaseLoadInfo info = assetBaseLoadInfos[i];
            UnityEngine.Object asset = EditorAssetLoader.LoadAssetAtPath(
                info.bundleName,
                info.assetName,
                info.expectedType);
            info._callback?.Invoke(asset);
        }

        assetBaseLoadInfos.Clear();
    }
#endif

    private int LoadAsset(string bundleName, string assetName, AssetLoadCallback callback = null, LoadPriority priority = LoadPriority.Medium, Type expectedType = null)
    {
        if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
        {
            GameLogController.Error("AB包名或资源名为空", "AssetBundleManager");
            callback?.Invoke(null);
            return -1;
        }

        GameLogController.Log($"开始尝试加载{bundleName}和{assetName}", "AssetBundleManager");

        // 查找或创建BundleInfo
        BundleInfo bundleInfo = GetOrCreateBundleInfo(bundleName);
        bundleInfo.AddReference();

        // 查找或创建AssetItem
        AssetItem assetItem = bundleInfo.GetOrCreateAssetItem(assetName);
        if (expectedType != null)
        {
            assetItem.expectedType = expectedType;
        }
        assetItem.AddReference();

        int callbackId = -1;
        if (callback != null)
        {
            callbackId = assetItem.AddCallback(callback);
        }

        string assetKey = GetAssetLoadKey(bundleName, assetName);
        _cancelledAssetKeys.Remove(assetKey);

        // 如果资源已经加载，直接返回
        if (assetItem.assetObject != null)
        {
            if (expectedType == null || expectedType.IsInstanceOfType(assetItem.assetObject))
            {
                assetItem.ExecuteCallbacks();
                return callbackId;
            }

            assetItem.assetObject = null;
        }

        // 如果资源正在加载，等待完成
        if (assetItem.loadRequest != null)
        {
            if (!assetItem.loadRequest.isDone)
            {
                return callbackId;
            }

            assetItem.loadRequest = null;
        }

        // 如果AB包已加载，将资源加入加载队列
        if (bundleInfo.isLoaded)
        {
            StartAssetLoading(assetItem);
            return callbackId;
        }

        // AB包未加载，设置优先级并加载AB包
        if (!bundleInfo.isLoading)
        {
            bundleInfo.loadPriority = priority;
            bundleInfo.AddPendingAsset(assetName);

            // 检查依赖并加载AB包
            LoadBundleWithDependencies(bundleName, priority);
        }
        else
        {
            // AB包正在加载，将资源加入待加载列表
            bundleInfo.AddPendingAsset(assetName);
        }


        return callbackId;
    }

    // 新增：取消特定资源的加载回调
    public void CancelAssetLoad(string bundleName, string assetName, int callbackId)
    {
#if UNITY_EDITOR
        if (loadMode == LoadMode.Development || loadMode == LoadMode.DebugMode)
        {
            //目前暂不提供
            return;
        }
#endif
        var assetItem = GetAssetItem(bundleName, assetName);
        if (assetItem != null)
        {
            // 减少引用计数（因为取消了资源加载）
            assetItem.RemoveCallback(callbackId);
            assetItem.RemoveReference();

            var bundleInfo = GetBundleInfo(bundleName);
            bundleInfo?.RemoveReference();

            // 无剩余回调且资源未落地：标记取消（含 loadRequest 进行中的异步加载）
            if (assetItem.GetCallbackCount() == 0 && assetItem.assetObject == null)
            {
                RemoveAssetFromLoadingQueue(assetItem);
            }
        }
    }

    //// 新增：取消特定资源的加载回调（通过委托实例）
    //public void CancelAssetLoad(string bundleName, string assetName, AssetLoadCallback callback)
    //{
    //    var assetItem = GetAssetItem(bundleName, assetName);
    //    if (assetItem != null)
    //    {
    //        assetItem.RemoveCallback(callback);

    //        // 如果没有回调且资源未加载，减少引用计数
    //        if (assetItem.GetCallbackCount() == 0 && assetItem.assetObject == null && assetItem.loadRequest == null)
    //        {
    //            // 从加载队列中移除
    //            RemoveAssetFromLoadingQueue(assetItem);

    //            // 减少引用计数（因为取消了资源加载）
    //            assetItem.RemoveReference();
    //            var bundleInfo = GetBundleInfo(bundleName);
    //            bundleInfo?.RemoveReference();
    //        }
    //    }
    //}

    //// 新增：取消资源的所有加载回调
    //public void CancelAllAssetCallbacks(string bundleName, string assetName)
    //{
    //    var assetItem = GetAssetItem(bundleName, assetName);
    //    if (assetItem != null)
    //    {
    //        assetItem.RemoveAllCallbacks();

    //        // 如果资源未加载，减少引用计数
    //        if (assetItem.assetObject == null && assetItem.loadRequest == null)
    //        {
    //            // 从加载队列中移除
    //            RemoveAssetFromLoadingQueue(assetItem);

    //            // 减少引用计数（因为取消了资源加载）
    //            assetItem.RemoveReference();
    //            var bundleInfo = GetBundleInfo(bundleName);
    //            bundleInfo?.RemoveReference();
    //        }
    //    }
    //}

    // 新增：从加载队列中移除资源
    private void RemoveAssetFromLoadingQueue(AssetItem assetItem)
    {
        string assetKey = GetAssetLoadKey(assetItem.bundleName, assetItem.assetName);
        // 惰性取消：不重建队列，不强行操作进行中的 request，交给 Update 时跳过/回收。
        _cancelledAssetKeys.Add(assetKey);
        _queuedAssetKeys.Remove(assetKey);

        // 从AB包的待加载列表中移除
        var bundleInfo = GetBundleInfo(assetItem.bundleName);
        bundleInfo?.RemovePendingAsset(assetItem.assetName);
    }

    // 卸载资源
    public void UnloadAsset(string bundleName, string assetName, bool forceUnload = false, int loadId = -1)
    {
        var assetItem = GetAssetItem(bundleName, assetName);
        if (assetItem != null)
        {
            if (loadId != -1) assetItem.RemoveCallback(loadId);
            assetItem.RemoveReference();

            // 减少所属AB包的引用计数
            var bundleInfo = GetBundleInfo(bundleName);
            if (bundleInfo != null)
            {
                bundleInfo.RemoveReference();
            }

            // 如果引用计数为0，可以考虑卸载资源（但保留在内存中供后续快速加载）
            if (forceUnload && assetItem.loadRequest == null && assetItem.referenceCount <= 0)
            {
                bundleInfo?.assetItems.Remove(assetName);
            }
        }
    }

    private void RemoveBundleAndDependenciesReferences(string bundleName)
    {
        if (!_allBundleDic.TryGetValue(bundleName, out BundleInfo bundleInfo))
        {
            GameLogController.Error($"Bundle依赖没找到，怎么去减少依赖？{bundleName}", "AssetBundleManager");
            return;
        }

        // 减少自身引用计数
        bundleInfo.RemoveReference();

        // 减少所有依赖的引用计数
        if (bundleInfo.Dependencies != null)
        {
            foreach (string dependency in bundleInfo.Dependencies)
            {
                if (bundleInfo.HasReferencedDependency(dependency) &&
                    _allBundleDic.TryGetValue(dependency, out BundleInfo depBundle))
                {
                    depBundle.RemoveReference(); // 减少依赖包的引用计数
                }
            }
        }
    }

    // 卸载AB包
    public void UnloadBundle(string bundleName, bool forceUnload = false)
    {
        if (_allBundleDic.TryGetValue(bundleName, out BundleInfo bundleInfo))
        {
            if (forceUnload || bundleInfo.referenceCount <= 0)
            {
                // 在卸载前先减少依赖的引用计数
                RemoveBundleAndDependenciesReferences(bundleName);

                bundleInfo.Unload(true);
                _loadedBundles.Remove(bundleName);
                GameLogController.Log($"卸载AB包: {bundleName}", "AssetBundleManager");
            }
        }
    }

    //// 强制回收所有未使用的AB包
    //public void ForceUnloadUnusedBundles()
    //{
    //    List<string> toUnload = new List<string>();

    //    foreach (var kvp in _loadedBundles)
    //    {
    //        if (kvp.Value.referenceCount <= 0)
    //        {
    //            toUnload.Add(kvp.Key);
    //        }
    //    }

    //    foreach (string bundleName in toUnload)
    //    {
    //        UnloadBundle(bundleName, true);
    //    }

    //    Debug.Log($"强制回收了 {toUnload.Count} 个未使用的AB包");
    //}

    #endregion

    #region 核心加载逻辑

    private BundleInfo GetOrCreateBundleInfo(string bundleName)
    {
        if (_allBundleDic.TryGetValue(bundleName, out BundleInfo bundleInfo))
        {
            return bundleInfo;
        }

        // 创建新的BundleInfo并设置依赖信息
        bundleInfo = new BundleInfo(bundleName);

        // 如果有清单信息，预先设置依赖
        if (_bundleManifests.TryGetValue(bundleName, out CustomAssetBundleInfo manifest))
        {
            if (manifest.Dependencies != null)
            {
                bundleInfo.Dependencies.AddRange(manifest.Dependencies);
            }
        }

        _allBundleDic.Add(bundleName, bundleInfo);

        return bundleInfo;
    }

    private void LoadBundleWithDependencies(string bundleName, LoadPriority priority)
    {
        if (!_bundleManifests.TryGetValue(bundleName, out CustomAssetBundleInfo manifest))
        {
            GameLogController.Error($"AB包不在清单中: {bundleName}", "AssetBundleManager");
            return;
        }

        // 检查本地是否存在（持久化目录或 StreamingAssets）
        if (!AssetBundlePathHelper.IsBundleAvailableAtRuntime(bundleName))
        {
            DownloadBundle(bundleName, (bool isSuccess, string message) =>
            {
                if (isSuccess)
                {
                    // 下载完成后重新尝试加载
                    LoadBundleWithDependencies(bundleName, priority);
                    return;
                }

                GameLogController.Error(
                    $"按需下载 AB 失败: {bundleName}, {message}",
                    "AssetBundleManager");
                OnBundleLoadFailed?.Invoke(bundleName);

                BundleInfo failedInfo = GetBundleInfo(bundleName);
                if (failedInfo == null)
                {
                    return;
                }

                foreach (string assetName in failedInfo.pendingAssets.ToList())
                {
                    var assetItem = failedInfo.GetAssetItem(assetName);
                    if (assetItem == null)
                    {
                        continue;
                    }

                    assetItem.loadRequest = null;
                    assetItem.assetObject = null;
                    assetItem.RemoveReference();
                    failedInfo.RemoveReference();
                    assetItem.ExecuteCallbacks();
                }

                failedInfo.pendingAssets.Clear();
            });
            return;
        }

        BundleInfo bundleInfo = GetOrCreateBundleInfo(bundleName);
        bundleInfo.loadPriority = priority;

        // 设置依赖列表
        if (manifest.Dependencies != null && manifest.Dependencies.Length > 0)
        {
            bundleInfo.Dependencies.Clear();
            bundleInfo.Dependencies.AddRange(manifest.Dependencies);
        }
        bundleInfo.ResetDependencyLoadState();

        // 设置依赖加载完成的回调
        bundleInfo.onDependenciesLoaded = () =>
        {
            // 所有依赖加载完成后才加载自身
            LoadBundleInternal(bundleName, priority);
        };

        // 检查并加载依赖
        LoadDependencies(bundleInfo, priority);
    }

    private void LoadDependencies(BundleInfo bundleInfo, LoadPriority priority)
    {
        if (bundleInfo.Dependencies == null || bundleInfo.Dependencies.Count == 0)
        {
            // 没有依赖，直接标记为依赖已加载
            bundleInfo.areDependenciesLoaded = true;
            bundleInfo.onDependenciesLoaded?.Invoke();
            return;
        }

        bool allDependenciesLoaded = true;

        foreach (string dependency in bundleInfo.Dependencies)
        {
            // 检查依赖是否已经加载
            if (_loadedBundles.TryGetValue(dependency, out BundleInfo depBundle))
            {
                // 依赖已加载，仅在父包尚未计入时增加引用计数，避免重复累加。
                if (bundleInfo.TryMarkDependencyReferenced(dependency))
                {
                    depBundle.AddReference();
                }
                bundleInfo.OnDependencyLoaded(dependency);
            }
            else
            {
                // 依赖未加载，开始加载
                allDependenciesLoaded = false;
                LoadDependencyBundle(dependency, bundleInfo, priority);
            }
        }

        // 如果所有依赖都已经加载完成
        if (allDependenciesLoaded)
        {
            bundleInfo.areDependenciesLoaded = true;
            bundleInfo.onDependenciesLoaded?.Invoke();
        }
    }

    private void LoadDependencyBundle(string dependencyName, BundleInfo parentBundle, LoadPriority priority)
    {
        // 依赖引用按父包去重计入，避免多次请求导致计数膨胀。
        var dependencyBundle = GetOrCreateBundleInfo(dependencyName);
        if (parentBundle.TryMarkDependencyReferenced(dependencyName))
        {
            dependencyBundle.AddReference(); // 因为被依赖而增加引用计数
        }

        // 递归加载依赖的依赖
        LoadBundleWithDependencies(dependencyName, priority);

        // 监听依赖包加载完成 / 失败
        System.Action<string> onDependencyLoaded = null;
        System.Action<string> onDependencyFailed = null;

        onDependencyLoaded = (bundleName) =>
        {
            if (bundleName == dependencyName)
            {
                parentBundle.OnDependencyLoaded(dependencyName);
                OnBundleLoaded -= onDependencyLoaded;
                OnBundleLoadFailed -= onDependencyFailed;
            }
        };

        onDependencyFailed = (bundleName) =>
        {
            if (bundleName != dependencyName)
            {
                return;
            }

            OnBundleLoaded -= onDependencyLoaded;
            OnBundleLoadFailed -= onDependencyFailed;

            // 依赖失败：通知父包失败，避免资源回调永久挂起。
            GameLogController.Error(
                $"依赖包加载失败: {dependencyName}，父包 {parentBundle.bundleName} 无法继续",
                "AssetBundleManager");
            OnBundleLoadFailed?.Invoke(parentBundle.bundleName);

            foreach (string assetName in parentBundle.pendingAssets.ToList())
            {
                var assetItem = parentBundle.GetAssetItem(assetName);
                if (assetItem == null)
                {
                    continue;
                }

                assetItem.loadRequest = null;
                assetItem.assetObject = null;
                assetItem.RemoveReference();
                parentBundle.RemoveReference();
                assetItem.ExecuteCallbacks();
            }

            parentBundle.pendingAssets.Clear();
            parentBundle.onDependenciesLoaded = null;
        };

        OnBundleLoaded += onDependencyLoaded;
        OnBundleLoadFailed += onDependencyFailed;
    }

    // 新增方法：内部加载AB包逻辑
    private void LoadBundleInternal(string bundleName, LoadPriority priority)
    {
        // 如果已经加载或正在加载，只增加引用计数（已经在LoadAsset中处理）
        if (_loadedBundles.TryGetValue(bundleName, out BundleInfo loadedBundle) ||
            _loadingBundleNames.Contains(bundleName))
        {
            return;
        }

        BundleInfo bundleInfo = GetOrCreateBundleInfo(bundleName);

        // 检查依赖是否全部加载完成
        if (!bundleInfo.areDependenciesLoaded && bundleInfo.Dependencies.Count > 0)
        {
            GameLogController.Warning($"尝试加载AB包 {bundleName} 但依赖尚未全部加载完成", "AssetBundleManager");
            return;
        }

        bundleInfo.loadPriority = priority;

        AddBundleToQueue(bundleInfo, priority);
    }

    private void StartAssetLoading(AssetItem assetItem)
    {
        var bundleInfo = GetBundleInfo(assetItem.bundleName);
        if (bundleInfo == null || !bundleInfo.isLoaded)
        {
            GameLogController.Warning($"AB包未加载，无法加载资源: {assetItem.assetName}", "AssetBundleManager");
            return;
        }

        // 如果资源已经在加载队列或正在加载，不再重复添加
        string assetKey = GetAssetLoadKey(assetItem.bundleName, assetItem.assetName);
        if (_queuedAssetKeys.Contains(assetKey) || _loadingAssetKeys.Contains(assetKey))
        {
            return;
        }

        // 将资源加入加载队列
        _assetLoadingQueue.Enqueue(assetItem);
        _queuedAssetKeys.Add(assetKey);
        _cancelledAssetKeys.Remove(assetKey);
        bundleInfo.RemovePendingAsset(assetItem.assetName);

        GameLogController.Log($"资源加入加载队列: {assetItem.assetName}, 队列位置: {_assetLoadingQueue.Count}", "AssetBundleManager");
    }

    // 新增方法：立即开始加载资源（不经过队列）
    private void StartAssetLoadingImmediate(AssetItem assetItem)
    {
        var bundleInfo = GetBundleInfo(assetItem.bundleName);
        if (bundleInfo == null || !bundleInfo.isLoaded)
        {
            GameLogController.Warning($"AB包未加载，无法加载资源: {assetItem.assetName}", "AssetBundleManager");
            assetItem.ExecuteCallbacks();
            return;
        }

        // 开始异步加载资源
        if (assetItem.expectedType != null)
        {
            assetItem.loadRequest = bundleInfo.bundle.LoadAssetAsync(assetItem.assetName, assetItem.expectedType);
        }
        else
        {
            assetItem.loadRequest = bundleInfo.bundle.LoadAssetAsync(assetItem.assetName);
        }
        _loadingAssets.Add(assetItem);
        _loadingAssetKeys.Add(GetAssetLoadKey(assetItem.bundleName, assetItem.assetName));

        GameLogController.Log($"开始加载资源: {assetItem.assetName}, 正在加载的资源数: {_loadingAssets.Count}", "AssetBundleManager");
    }

    // 新增方法：检查资源是否在加载队列中
    private bool IsAssetInLoadingQueue(AssetItem assetItem)
    {
        return _queuedAssetKeys.Contains(GetAssetLoadKey(assetItem.bundleName, assetItem.assetName));
    }

    #endregion

    #region 更新循环

    // 更新AB包加载状态
    private void UpdateBundleLoading()
    {
        int currentLoading = _loadingBundles.Count;

        if (currentLoading < maxConcurrentLoads)
        {
            BundleInfo nextBundle = GetNextBundleToLoad();
            if (nextBundle != null)
            {
                StartLoadingBundle(nextBundle);
            }
        }

        for (int i = _loadingBundles.Count - 1; i >= 0; i--)
        {
            var bundleInfo = _loadingBundles[i];
            if (bundleInfo.bundleRequest != null && bundleInfo.bundleRequest.isDone)
            {
                OnBundleLoadComplete(bundleInfo);
                _loadingBundles.RemoveAt(i);
            }
        }
    }

    // 新增方法：更新资源加载状态
    private void UpdateAssetLoading()
    {
        // 检查当前加载数量，如果未达到上限，从队列中取出资源开始加载
        while (_loadingAssets.Count < maxConcurrentAssetLoads && _assetLoadingQueue.Count > 0)
        {
            AssetItem nextAsset = _assetLoadingQueue.Dequeue();
            string assetKey = GetAssetLoadKey(nextAsset.bundleName, nextAsset.assetName);
            _queuedAssetKeys.Remove(assetKey);
            if (_cancelledAssetKeys.Contains(assetKey))
            {
                continue;
            }
            StartAssetLoadingImmediate(nextAsset);
        }

        // 更新正在加载的资源状态
        for (int i = _loadingAssets.Count - 1; i >= 0; i--)
        {
            AssetItem assetItem = _loadingAssets[i];
            if (assetItem.loadRequest != null && assetItem.loadRequest.isDone)
            {
                OnAssetLoadComplete(assetItem);
                _loadingAssets.RemoveAt(i);
            }
        }
    }

    // 获取下一个要加载的AB包
    private BundleInfo GetNextBundleToLoad()
    {
        if (_highPriorityQueue.Count > 0)
            return _highPriorityQueue.Dequeue();
        if (_mediumPriorityQueue.Count > 0)
            return _mediumPriorityQueue.Dequeue();
        if (_lowPriorityQueue.Count > 0)
            return _lowPriorityQueue.Dequeue();

        return null;
    }

    // 修改StartLoadingBundle方法，添加依赖检查
    private void StartLoadingBundle(BundleInfo bundleInfo)
    {
        if (bundleInfo.isLoading || bundleInfo.isLoaded)
            return;

        // 再次确认依赖已全部加载
        if (!bundleInfo.areDependenciesLoaded && bundleInfo.Dependencies.Count > 0)
        {
            GameLogController.Warning($"AB包 {bundleInfo.bundleName} 的依赖尚未全部加载完成，无法开始加载", "AssetBundleManager");
            return;
        }

        string path = AssetBundlePathHelper.GetRuntimeLoadPath(bundleInfo.bundleName);

        if (bundleInfo.loadPriority == LoadPriority.Sync)
        {
            // 同步加载：不进入 _loadingBundles，避免占死并发槽。
            bundleInfo.isLoading = true;
            _loadingBundleNames.Add(bundleInfo.bundleName);
            bundleInfo.bundle = AssetBundle.LoadFromFile(path);
            OnBundleLoadComplete(bundleInfo);
            return;
        }

        bundleInfo.isLoading = true;
        _loadingBundles.Add(bundleInfo);
        _loadingBundleNames.Add(bundleInfo.bundleName);
        bundleInfo.bundleRequest = AssetBundle.LoadFromFileAsync(path);

        GameLogController.Log($"开始加载AB包: {bundleInfo.bundleName}, 优先级: {bundleInfo.loadPriority}, 依赖数: {bundleInfo.Dependencies.Count}", "AssetBundleManager");
    }

    // 修改OnBundleLoadComplete方法，在AB包加载失败时减少引用计数
    private void OnBundleLoadComplete(BundleInfo bundleInfo)
    {
        bundleInfo.isLoading = false;
        _loadingBundleNames.Remove(bundleInfo.bundleName);

        AssetBundle loadedBundle = bundleInfo.bundleRequest?.assetBundle ?? bundleInfo.bundle;

        if (loadedBundle != null)
        {
            bundleInfo.bundle = loadedBundle;
            bundleInfo.isLoaded = true;
            bundleInfo.loadTime = DateTime.Now;

            _loadedBundles[bundleInfo.bundleName] = bundleInfo;
            GameLogController.Log($"AB包加载完成: {bundleInfo.bundleName}, 等待加载资源数: {bundleInfo.pendingAssets.Count}", "AssetBundleManager");

            // 加载所有等待中的资源
            foreach (string assetName in bundleInfo.pendingAssets.ToList())
            {
                var assetItem = bundleInfo.GetAssetItem(assetName);
                if (assetItem != null && assetItem.assetObject == null)
                {
                    StartAssetLoading(assetItem);
                }
            }

            OnBundleLoaded?.Invoke(bundleInfo.bundleName);
        }
        else
        {
            GameLogController.Error($"AB包加载失败: {bundleInfo.bundleName}", "AssetBundleManager");
            OnBundleLoadFailed?.Invoke(bundleInfo.bundleName);
            // AB包加载失败时，必须通知等待中的资源请求方，避免上层逻辑一直等待。
            foreach (string assetName in bundleInfo.pendingAssets.ToList())
            {
                var assetItem = bundleInfo.GetAssetItem(assetName);
                if (assetItem != null)
                {
                    assetItem.loadRequest = null;
                    assetItem.assetObject = null;

                    // 收敛一次引用，避免失败路径上长期堆积。
                    assetItem.RemoveReference();
                    bundleInfo.RemoveReference();

                    assetItem.ExecuteCallbacks();
                }
            }
        }

        bundleInfo.pendingAssets.Clear();
    }

    private void OnAssetLoadComplete(AssetItem assetItem)
    {
        string assetKey = GetAssetLoadKey(assetItem.bundleName, assetItem.assetName);
        bool isCancelled = _cancelledAssetKeys.Contains(assetKey);
        _loadingAssetKeys.Remove(assetKey);

        if (isCancelled)
        {
            _cancelledAssetKeys.Remove(assetKey);
            assetItem.loadRequest = null;

            if (assetItem.GetCallbackCount() > 0)
            {
                //RetryAssetLoadAfterCancel(assetItem);
            }

            return;
        }

        if (assetItem.loadRequest.asset != null)
        {
            UnityEngine.Object loadedAsset = assetItem.loadRequest.asset;
            if (assetItem.expectedType != null && !assetItem.expectedType.IsInstanceOfType(loadedAsset))
            {
                var bundleInfo = GetBundleInfo(assetItem.bundleName);
                UnityEngine.Object resolvedAsset = bundleInfo?.bundle != null
                    ? TryResolveTypedAsset(bundleInfo.bundle, assetItem.assetName, assetItem.expectedType)
                    : null;
                if (resolvedAsset != null)
                {
                    loadedAsset = resolvedAsset;
                }
            }

            assetItem.assetObject = loadedAsset;
            GameLogController.Log($"资源加载完成: {assetItem.assetName}", "AssetBundleManager");
        }
        else
        {
            var bundleInfo = GetBundleInfo(assetItem.bundleName);
            if (bundleInfo?.bundle != null && assetItem.expectedType != null)
            {
                UnityEngine.Object resolvedAsset = TryResolveTypedAsset(
                    bundleInfo.bundle,
                    assetItem.assetName,
                    assetItem.expectedType);
                if (resolvedAsset != null)
                {
                    assetItem.assetObject = resolvedAsset;
                    GameLogController.Log($"资源加载完成: {assetItem.assetName}", "AssetBundleManager");
                    assetItem.loadRequest = null;
                    assetItem.ExecuteCallbacks();
                    return;
                }
            }

            GameLogController.Error($"资源加载失败: {assetItem.assetName}", "AssetBundleManager");

            // 资源加载失败，减少引用计数
            assetItem.RemoveReference();

            var bundleInfos = GetBundleInfo(assetItem.bundleName);
            bundleInfos?.RemoveReference();
        }

        assetItem.loadRequest = null;

        assetItem.ExecuteCallbacks();
    }

    private static string GetAssetLoadKey(string bundleName, string assetName)
    {
        return (bundleName + "|" + assetName).ToLowerInvariant();
    }

    //// 新增方法：批量释放多个资源
    //public void ReleaseAssets(List<(string bundleName, string assetName)> assets)
    //{
    //    foreach (var (bundleName, assetName) in assets)
    //    {
    //        UnloadAsset(bundleName, assetName);
    //    }
    //}

    //// 新增方法：释放整个AB包的所有资源
    //public void ReleaseAllAssetsInBundle(string bundleName)
    //{
    //    var bundleInfo = GetBundleInfo(bundleName);
    //    if (bundleInfo != null)
    //    {
    //        foreach (var assetItem in bundleInfo.assetItems.Values.ToList())
    //        {
    //            UnloadAsset(bundleName, assetItem.assetName);
    //        }
    //    }
    //}

    #endregion

    #region 工具方法

    void RetryAssetLoadAfterCancel(AssetItem assetItem)
    {
        BundleInfo bundleInfo = GetOrCreateBundleInfo(assetItem.bundleName);
        if (bundleInfo.isLoaded)
        {
            StartAssetLoading(assetItem);
            return;
        }

        if (bundleInfo.isLoading)
        {
            bundleInfo.AddPendingAsset(assetItem.assetName);
            return;
        }

        bundleInfo.AddPendingAsset(assetItem.assetName);
        LoadBundleWithDependencies(assetItem.bundleName, LoadPriority.Medium);
    }

    static UnityEngine.Object TryResolveTypedAsset(AssetBundle bundle, string assetName, Type expectedType)
    {
        if (bundle == null || expectedType == null)
        {
            return null;
        }

        UnityEngine.Object[] allAssets = bundle.LoadAllAssets<UnityEngine.Object>();
        for (int i = 0; i < allAssets.Length; i++)
        {
            UnityEngine.Object obj = allAssets[i];
            if (obj != null && obj.name == assetName && expectedType.IsInstanceOfType(obj))
            {
                return obj;
            }
        }

        for (int i = 0; i < allAssets.Length; i++)
        {
            UnityEngine.Object obj = allAssets[i];
            if (obj != null && expectedType.IsInstanceOfType(obj))
            {
                return obj;
            }
        }

        for (int i = 0; i < allAssets.Length; i++)
        {
            UnityEngine.Object obj = allAssets[i];
            if (obj != null && obj.name == assetName)
            {
                return obj;
            }
        }

        return null;
    }

    // 将AB包加入加载队列
    private void AddBundleToQueue(BundleInfo bundleInfo, LoadPriority priority)
    {
        if (bundleInfo.isLoading || bundleInfo.isLoaded || _loadingBundleNames.Contains(bundleInfo.bundleName))
        {
            return;
        }

        // 已在任一优先级队列中则不再重复入队。
        if (IsBundleInAnyQueue(bundleInfo))
        {
            return;
        }

        switch (priority)
        {
            case LoadPriority.Low:
                _lowPriorityQueue.Enqueue(bundleInfo);
                break;
            case LoadPriority.Medium:
                _mediumPriorityQueue.Enqueue(bundleInfo);
                break;
            case LoadPriority.High:
                _highPriorityQueue.Enqueue(bundleInfo);
                break;
            case LoadPriority.Sync:
                // 同步加载立即执行
                StartLoadingBundle(bundleInfo);
                break;
        }
    }

    private bool IsBundleInAnyQueue(BundleInfo bundleInfo)
    {
        return _lowPriorityQueue.Contains(bundleInfo)
            || _mediumPriorityQueue.Contains(bundleInfo)
            || _highPriorityQueue.Contains(bundleInfo);
    }

    // 下载AB包（按需补齐缺失文件）
    private void DownloadBundle(string bundleName, System.Action<bool, string> onDownloadComplete)
    {
        if (AssetBundleDownloader.Instance == null)
        {
            GameLogController.Error("AB下载器未设置", "AssetBundleManager");
            onDownloadComplete?.Invoke(false, "Downloader missing");
            return;
        }

        // CompressedFormat==0（LZMA）时需要下载后转 LZ4。
        bool needConvert = _currentManifest != null && _currentManifest.CompressedFormat == 0;
        GameLogController.Log($"开始下载AB包: {bundleName}, needConvert={needConvert}", "AssetBundleManager");
        AssetBundleDownloader.Instance.DownloadBundle(bundleName, needConvert, onDownloadComplete);
    }

    // 卸载检查协程
    private IEnumerator UnloadCheckCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(unloadCheckInterval);
            CheckAndUnloadUnusedBundles();
        }
    }

    // 检查并卸载未使用的AB包
    private void CheckAndUnloadUnusedBundles()
    {
        int unloadedCount = 0;
        List<string> toUnload = new List<string>();

        foreach (var kvp in _loadedBundles)
        {
            var bundleInfo = kvp.Value;
            if (bundleInfo.referenceCount <= 0)
            {
                TimeSpan timeSinceLastUse = DateTime.Now - bundleInfo.lastUseTime;
                if (timeSinceLastUse.TotalSeconds > unloadReferenceThreshold)
                {
                    toUnload.Add(kvp.Key);
                }
            }
        }

        foreach (string bundleName in toUnload)
        {
            UnloadBundle(bundleName, true);
            unloadedCount++;
        }

        if (unloadedCount > 0)
        {
            GameLogController.Log($"自动卸载了 {unloadedCount} 个未使用的AB包", "AssetBundleManager");
        }
    }

    #endregion

    #region 查询方法

    public BundleInfo GetBundleInfo(string bundleName)
    {
        _allBundleDic.TryGetValue(bundleName, out BundleInfo bundleInfo);
        return bundleInfo;
    }

    public AssetItem GetAssetItem(string bundleName, string assetName)
    {
        var bundleInfo = GetBundleInfo(bundleName);
        return bundleInfo?.GetAssetItem(assetName);
    }

    // 新增：获取资源的回调数量（用于调试）
    public int GetAssetCallbackCount(string bundleName, string assetName)
    {
        var assetItem = GetAssetItem(bundleName, assetName);
        return assetItem?.GetCallbackCount() ?? 0;
    }

    #endregion
}