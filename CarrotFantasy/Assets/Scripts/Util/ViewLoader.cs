using System;
using System.Collections.Generic;
using UnityEngine;

enum ViewLoadState
{
    None,
    Loading,
    Loaded
}

class ViewLoadEntry
{
    public string bundleName;
    public string assetName;
    public GameObject gameObject;
    public ViewLoadState state;
    public int order;
    public int loadIndex;
}

/// <summary>BaseView 的 AB 子资源加载、实例化与 index 级加载状态。</summary>
class ViewLoader
{
    private readonly Func<bool> _isViewOpen;
    private readonly Func<Transform> _getInstantiateParent;
    private readonly Action<int, IReadOnlyList<GameObject>> _onIndexLoadComplete;

    private int _layerOrder;
    private readonly Dictionary<int, List<ViewLoadEntry>> _entriesByIndex = new Dictionary<int, List<ViewLoadEntry>>();
    private readonly Dictionary<int, ViewLoadState> _indexState = new Dictionary<int, ViewLoadState>();
    private readonly Queue<int> _loadQueue = new Queue<int>();
    private readonly List<GameObject> _indexInstanceBuffer = new List<GameObject>(4);

    public ViewLoader(
        Func<bool> isViewOpen,
        Func<Transform> getInstantiateParent,
        Action<int, IReadOnlyList<GameObject>> onIndexLoadComplete)
    {
        _isViewOpen = isViewOpen;
        _getInstantiateParent = getInstantiateParent;
        _onIndexLoadComplete = onIndexLoadComplete;
    }

    public void RegisterAsset(int index, string bundle, string asset)
    {
        _layerOrder++;
        if (!_entriesByIndex.TryGetValue(index, out List<ViewLoadEntry> entries))
        {
            entries = new List<ViewLoadEntry>();
            _entriesByIndex.Add(index, entries);
        }

        entries.Add(new ViewLoadEntry
        {
            assetName = asset,
            bundleName = bundle,
            order = _layerOrder,
            state = ViewLoadState.None,
        });
    }

    public void ClearIndexStates()
    {
        _indexState.Clear();
    }

    public void ClearQueue()
    {
        _loadQueue.Clear();
    }

    public bool TryGetIndexState(int index, out ViewLoadState state)
    {
        return _indexState.TryGetValue(index, out state);
    }

    public ViewLoadState GetIndexStateOrDefault(int index)
    {
        return _indexState.GetValueOrDefault(index, ViewLoadState.None);
    }

    public bool IsIndexLoaded(int index)
    {
        return GetIndexStateOrDefault(index) == ViewLoadState.Loaded;
    }

    public void FixStaleLoadingState(int index)
    {
        if (GetIndexStateOrDefault(index) == ViewLoadState.Loading && !HasInFlightLoadForIndex(index))
        {
            _indexState[index] = ViewLoadState.None;
        }
    }

    public bool IsFullyInstantiated(int index)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(index, null);
        if (entries == null || entries.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; ++i)
        {
            ViewLoadEntry entry = entries[i];
            if (entry.state != ViewLoadState.Loaded || entry.gameObject == null)
            {
                return false;
            }
        }

        return true;
    }

    public void MarkIndexLoaded(int index)
    {
        _indexState[index] = ViewLoadState.Loaded;
    }

    public void RequestLoadRootIfNeeded()
    {
        if (!IsFullyInstantiated(0))
        {
            EnqueueIndexForLoad(0);
        }
    }

    public void RequestLoadIndexIfNeeded(int index)
    {
        if (index != 0 && !IsFullyInstantiated(index))
        {
            EnqueueIndexForLoad(index);
        }
    }

    public void ProcessQueue()
    {
        if (_loadQueue.Count == 0)
        {
            return;
        }

        int index = _loadQueue.Dequeue();
        LoadIndexAssets(index);
    }

    public void SetIndexVisible(int index, bool visible)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(index, null);
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; ++i)
        {
            GameObject go = entries[i].gameObject;
            if (go != null)
            {
                go.SetActive(visible);
            }
        }
    }

    public void DetachInstances()
    {
        foreach (KeyValuePair<int, List<ViewLoadEntry>> pair in _entriesByIndex)
        {
            List<ViewLoadEntry> entries = pair.Value;
            for (int i = 0; i < entries.Count; ++i)
            {
                entries[i].gameObject = null;
            }
        }
    }

    public void ReleaseAssets()
    {
        foreach (KeyValuePair<int, List<ViewLoadEntry>> pair in _entriesByIndex)
        {
            List<ViewLoadEntry> entries = pair.Value;
            for (int i = 0; i < entries.Count; ++i)
            {
                ViewLoadEntry entry = entries[i];
                if (entry.state == ViewLoadState.Loaded)
                {
                    AssetBundleManager.Instance.UnloadAsset(entry.bundleName, entry.assetName);
                }
                else if (entry.state == ViewLoadState.Loading)
                {
                    AssetBundleManager.Instance.CancelAssetLoad(entry.bundleName, entry.assetName, entry.loadIndex);
                }

                entry.state = ViewLoadState.None;
                entry.loadIndex = -1;
            }
        }

        _indexState.Clear();
        _loadQueue.Clear();
    }

    public void CancelInFlightLoads()
    {
        _loadQueue.Clear();
        HashSet<int> affectedIndices = new HashSet<int>();
        foreach (KeyValuePair<int, List<ViewLoadEntry>> pair in _entriesByIndex)
        {
            List<ViewLoadEntry> entries = pair.Value;
            for (int i = 0; i < entries.Count; ++i)
            {
                ViewLoadEntry entry = entries[i];
                if (entry.state != ViewLoadState.Loading)
                {
                    continue;
                }

                if (entry.loadIndex >= 0)
                {
                    AssetBundleManager.Instance.CancelAssetLoad(entry.bundleName, entry.assetName, entry.loadIndex);
                }

                entry.state = ViewLoadState.None;
                entry.loadIndex = -1;
                affectedIndices.Add(pair.Key);
            }
        }

        foreach (int index in affectedIndices)
        {
            RefreshIndexState(index);
        }
    }

    private void EnqueueIndexForLoad(int index)
    {
        if (!IsIndexInLoadQueue(index))
        {
            _loadQueue.Enqueue(index);
        }

        _indexState[index] = ViewLoadState.Loading;
    }

    private bool IsIndexInLoadQueue(int index)
    {
        foreach (int queuedIndex in _loadQueue)
        {
            if (queuedIndex == index)
            {
                return true;
            }
        }

        return false;
    }

    private void LoadIndexAssets(int index)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(index, null);
        if (entries == null)
        {
            return;
        }

        bool anyRequested = false;
        for (int i = 0; i < entries.Count; ++i)
        {
            ViewLoadEntry entry = entries[i];
            if (entry.state == ViewLoadState.Loaded && entry.gameObject != null)
            {
                continue;
            }

            anyRequested = true;
            entry.state = ViewLoadState.Loading;
            int loadId = AssetBundleManager.Instance.LoadAsset<GameObject>(
                entry.bundleName,
                entry.assetName,
                (GameObject obj) => { OnAssetLoaded(obj, entry, index); });
            entry.loadIndex = loadId;
            if (entry.state != ViewLoadState.Loading)
            {
                // 同步 CACHE_HIT 回调已处理完毕（含放弃路径）
                entry.loadIndex = -1;
            }
        }

        if (!anyRequested)
        {
            NotifyIndexLoadComplete(index);
        }
    }

    private void OnAssetLoaded(GameObject prefab, ViewLoadEntry entry, int targetIndex)
    {
        if (_isViewOpen() == false)
        {
            AbandonAssetLoad(entry);
            RefreshIndexState(targetIndex);
            ProcessQueue();
            return;
        }

        if (prefab == null)
        {
            entry.state = ViewLoadState.None;
            entry.loadIndex = -1;
            Debug.LogError("[ViewLoader] AB 资源加载失败: " + entry.bundleName + " / " + entry.assetName);
            RefreshIndexState(targetIndex);
            ProcessQueue();
            return;
        }

        Transform parent = _getInstantiateParent();
        if (parent == null)
        {
            AbandonAssetLoad(entry);
            RefreshIndexState(targetIndex);
            ProcessQueue();
            return;
        }

        GameObject instanceObj = GameObject.Instantiate(prefab, parent);
        instanceObj.SetActive(false);
        entry.gameObject = instanceObj;
        entry.state = ViewLoadState.Loaded;
        entry.loadIndex = -1;

        NotifyIndexLoadComplete(targetIndex);
    }

    private void NotifyIndexLoadComplete(int targetIndex)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(targetIndex, null);
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; ++i)
        {
            if (entries[i].state != ViewLoadState.Loaded)
            {
                return;
            }
        }

        _indexInstanceBuffer.Clear();
        for (int i = 0; i < entries.Count; ++i)
        {
            _indexInstanceBuffer.Add(entries[i].gameObject);
        }

        _indexState[targetIndex] = ViewLoadState.Loaded;
        _onIndexLoadComplete?.Invoke(targetIndex, _indexInstanceBuffer);
        ProcessQueue();
    }

    private void AbandonAssetLoad(ViewLoadEntry entry)
    {
        if (entry.loadIndex >= 0)
        {
            AssetBundleManager.Instance.UnloadAsset(entry.bundleName, entry.assetName, false, entry.loadIndex);
        }
        else
        {
            AssetBundleManager.Instance.UnloadAsset(entry.bundleName, entry.assetName);
        }

        entry.state = ViewLoadState.None;
        entry.loadIndex = -1;
    }

    private bool HasInFlightLoadForIndex(int index)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(index, null);
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; ++i)
        {
            if (entries[i].state == ViewLoadState.Loading)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshIndexState(int index)
    {
        List<ViewLoadEntry> entries = _entriesByIndex.GetValueOrDefault(index, null);
        if (entries == null || entries.Count == 0)
        {
            _indexState.Remove(index);
            return;
        }

        bool allLoaded = true;
        bool anyLoading = false;
        for (int i = 0; i < entries.Count; ++i)
        {
            ViewLoadEntry entry = entries[i];
            if (entry.state == ViewLoadState.Loading)
            {
                anyLoading = true;
            }

            if (entry.state != ViewLoadState.Loaded)
            {
                allLoaded = false;
            }
        }

        if (allLoaded)
        {
            _indexState[index] = ViewLoadState.Loaded;
        }
        else if (anyLoading)
        {
            _indexState[index] = ViewLoadState.Loading;
        }
        else
        {
            _indexState[index] = ViewLoadState.None;
        }
    }
}
