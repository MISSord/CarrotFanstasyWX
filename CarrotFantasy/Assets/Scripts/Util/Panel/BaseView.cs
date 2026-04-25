using System.Collections.Generic;
using UnityEngine;

class UIDownInfo
{
    public string bundleName;
    public string assetName;
    public UnityEngine.GameObject gameObject;
    public LoadState isLoaded;
    public int order;
    public int loadIndex;
}

enum LoadState
{
    None,
    Loading,
    Loaded
}


public abstract class BaseView
{
    protected string viewName = "viewName";
    public string ViewName
    {
        get { return viewName; }
    }

    protected UILayer layer = UILayer.Normal;
    public UILayer Layer
    {
        get
        {
            return layer;
        }
    }

    protected int defaultIndex = 0;
    protected int CurShowIndex = -1;
    protected bool isOpen = false;
    protected UINameTableDic nameTableDic;

    // 根对象：Instantiate 出来的整棵 UI 根
    private GameObject rootObject; // 根 GameObject
    private Transform rootView; // 子页面挂点（如 Root）
    private Canvas rootCanvas; // 根节点上的 Canvas
    private int layerOrder = 0;
    private bool isInitData = false;
    private bool isLoadRoot = false;
    private string delayReleaseId;

    /// <summary>index=0 的 UI 加载完成后，首个子物体的 transform，供与 BasePanel 等对接使用</summary>
    protected Transform transform;

    private Dictionary<int, List<UIDownInfo>> uiLoadInfoDic = new Dictionary<int, List<UIDownInfo>>();
    private Dictionary<int, LoadState> isLoadedDic = new Dictionary<int, LoadState>(); // 各 index 下子界面的整体加载状态
    private Dictionary<int, bool> isFirstOpenDic = new Dictionary<int, bool>(); // 各 index 是否已首次打开
    private Queue<int> needToLoadIndexQueue = new Queue<int>();
    private Dictionary<int, Dictionary<string, string>> flushInfo = new Dictionary<int, Dictionary<string, string>>();

    private bool isPausedByViewStack;

    // 子类实现：如 SetUILoadInfo、数据初始化等
    public abstract void InitData();

    public void RegisterData()
    {
        if (isInitData) return;
        nameTableDic = new UINameTableDic();
        this.InitData();
        ViewManager.Instance.RegisterView(this);
        isInitData = true;
    }

    public void DeleteMe()
    {
        nameTableDic.ClearAllInfo();
        needToLoadIndexQueue.Clear();
    }

    public void Release()
    {
        ReleaseCallBack();
        transform = null;

        // 先断开对实例的引用，再 Destroy
        foreach (var list in uiLoadInfoDic)
        {
            for (int i = 0; i < list.Value.Count; ++i)
            {
                UIDownInfo info = list.Value[i];
                info.gameObject = null;
            }
        }

        GameObject.Destroy(rootObject);
        rootObject = null;
        rootCanvas = null;
        rootView = null;

        // 卸载或取消已加载/加载中的 AB 子资源
        foreach (var info in uiLoadInfoDic)
        {
            List<UIDownInfo> list = info.Value;
            for (int i = 0; i < list.Count; ++i)
            {
                UIDownInfo downInfo = list[i];
                if (downInfo.isLoaded == LoadState.Loaded)
                {
                    AssetBundleManager.Instance.UnloadAsset(downInfo.bundleName, downInfo.assetName);
                }
                else if (downInfo.isLoaded == LoadState.Loading)
                {
                    AssetBundleManager.Instance.CancelAssetLoad(downInfo.bundleName, downInfo.assetName, downInfo.loadIndex);
                }
            }
        }

        // 清延迟释放标记
        delayReleaseId = null;
        CurShowIndex = -1;
        isLoadedDic.Clear();
        isFirstOpenDic.Clear();
        needToLoadIndexQueue.Clear();
        nameTableDic.ClearAllInfo();
        isLoadRoot = false;
    }

    protected void SetUILoadInfo(int index, string bundle, string asset)
    {
        layerOrder++;
        List<UIDownInfo> info;
        if (uiLoadInfoDic.TryGetValue(index, out info) == false)
        {
            info = new List<UIDownInfo>();
            uiLoadInfoDic.Add(index, info);
        }
        info.Add(new UIDownInfo()
        {
            assetName = asset,
            bundleName = bundle,
            order = layerOrder,
            isLoaded = LoadState.None,
        });
    }

    protected void ChangeIndex(int targetIndex)
    {
        // 已处于目标且已加载完成，则只 Flush
        if (CurShowIndex == targetIndex && isLoadedDic[CurShowIndex] == LoadState.Loaded)
        {
            Flush();
            return;
        }

        // 从当前子页切走时，先处理当前页显示/刷新
        if (CurShowIndex != 0 && CurShowIndex != -1 && isLoadedDic[CurShowIndex] == LoadState.Loaded)
        {
            TryFlushViewShow(CurShowIndex, false);
        }

        CurShowIndex = targetIndex;

        // 目标 index 的加载情况
        LoadState state = isLoadedDic.GetValueOrDefault(CurShowIndex, LoadState.None);
        if (state == LoadState.Loaded)
        {
            FlushShowView(targetIndex);
            return;
        }
        else
        {
            state = isLoadedDic.GetValueOrDefault(0, LoadState.None);
            // 根（index 0）未排过队则先入队拉取
            if (state == LoadState.None)
            {
                needToLoadIndexQueue.Enqueue(0);
                isLoadedDic[0] = LoadState.Loading;
            }

            state = isLoadedDic.GetValueOrDefault(targetIndex, LoadState.None);
            if (state == LoadState.None)
            {
                needToLoadIndexQueue.Enqueue(targetIndex);
                isLoadedDic[targetIndex] = LoadState.Loading;
            }
        }

        bool isFirstLoad = isFirstOpenDic.GetValueOrDefault(targetIndex, false);
        if (isFirstLoad != true)
        {
            isFirstOpenDic[targetIndex] = true;
            OpenCallBack(targetIndex);
        }

        CheckIsNeedLoad();
    }

    #region 可重写回调
    protected virtual void LoadCallBack() { }

    protected virtual void LoadIndexCallBack(int viewIndex) { }

    protected virtual void ShowIndexCallBack(int viewIndex) { }

    protected virtual void ReleaseCallBack() { }

    protected virtual void CloseCallBack() { }

    protected virtual void OnFlush(int index, Dictionary<string, string> info = null) { }

    protected virtual void OpenCallBack(int index) { }

    /// <summary> 本 View 在打开栈中不再处于最上层时，由 ViewManager 调用 </summary>
    protected virtual void OnPause() { }

    /// <summary> 再次成为最上层时恢复 </summary>
    protected virtual void OnResume() { }

    /// <summary> 与 FlushViewOrder 配合：在打开栈中排序后，最上层为 true </summary>
    public void ApplyViewStackPauseState(bool isTopmostInOpenStack)
    {
        if (isTopmostInOpenStack)
        {
            if (isPausedByViewStack)
            {
                isPausedByViewStack = false;
                OnResume();
            }
        }
        else
        {
            if (!isPausedByViewStack)
            {
                isPausedByViewStack = true;
                OnPause();
            }
        }
    }

    #endregion

    #region 公开接口

    public void TryFlushTargetIndex(int index, string key = null, string content = null)
    {
        if (key != null && content != null)
        {
            if (flushInfo.ContainsKey(index) == false)
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                flushInfo.Add(index, dic);
            }
            Dictionary<string, string> finDic = flushInfo[index];
            finDic[key] = content;
        }
    }

    public bool GetIsLoadedIndex(int index)
    {
        LoadState state = isLoadedDic.GetValueOrDefault(index, LoadState.None);
        return state == LoadState.Loaded;
    }

    public void Flush()
    {
        if (CurShowIndex != -1)
        {
            TimeUtility.Instance.SetTimeout(0f, () =>
            {
                Dictionary<string, string> info = flushInfo.GetValueOrDefault(CurShowIndex, null);
                this.OnFlush(CurShowIndex, info);
                info?.Clear();
            });
        }
    }

    public bool GetIsOpen()
    {
        return this.isOpen;
    }

    public void ChangeCurCanvaseOrder(int order)
    {
        if (rootCanvas != null) { rootCanvas.sortingOrder = order; }
    }

    public virtual void Open(int index = 0)
    {
        // 尚未建根且传入 0 时，用子类 defaultIndex
        if (index == 0 && isLoadRoot == false)
        {
            index = GetDefaultIndex();
        }

        if (delayReleaseId != null)
        {
            TimeUtility.Instance.RemoveTimeout(delayReleaseId);
            delayReleaseId = null;
        }

        ChangeIndex(index);

        if (isOpen == true)
        {
            // 已打开则先移出再插回，保证在打开列表尾部（最前显示）
            ViewManager.Instance.RemoveViewFromOpenList(this);
            ViewManager.Instance.AddOpenViewToOpenList(this);
            return;
        }
        else
        {
            ViewManager.Instance.AddOpenViewToOpenList(this);
        }

        if (isLoadRoot == false)
        {
            CreateViewRoot();
        }
        else
        {
            rootView.transform.localPosition = Vector3.zero;
        }

        isOpen = true;
    }

    public virtual void Close()
    {
        CloseCallBack();

        isOpen = false;
        if (isPausedByViewStack)
        {
            isPausedByViewStack = false;
            OnResume();
        }
        ViewManager.Instance.RemoveViewFromOpenList(this);
        rootView.transform.localPosition = new Vector2(99999, 99999);

        // 若已有未执行的延迟释放，避免重复排程
        if (delayReleaseId != null)
        {
            Debug.LogError($"[BaseView] 关闭时仍有一次延迟释放在排队，已忽略本次 Close: {delayReleaseId}");
            return;
        }

        string time = Time.unscaledTime.ToString();
        delayReleaseId = viewName + time;
        TimeUtility.Instance.SetTimeout(5f, this.Release, false, delayReleaseId);
    }

    #endregion

    #region 私有方法

    // 从 BaseView 预制体克隆出根节点
    private void CreateViewRoot()
    {
        GameObject baseView = ViewManager.Instance.GetBaseViewClone();
        rootObject = GameObject.Instantiate(baseView);
        rootObject.name = viewName;

        GameObject uiRoot = ViewManager.Instance.GetUIRoot();
        rootObject.transform.SetParent(uiRoot.transform);
        rootObject.transform.SetAsLastSibling();
        rootObject.transform.localPosition = Vector3.zero;

        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = ViewManager.Instance.GetUICamera();
        rootCanvas = canvas;

        // 子页面实例会挂到名为 Root 的节点下
        rootView = rootObject.transform.Find("Root");
        isLoadRoot = true;
    }

    private int GetDefaultIndex()
    {
        return this.defaultIndex;
    }

    private void TryFlushViewShow(int index, bool state)
    {
        List<UIDownInfo> infos = uiLoadInfoDic.GetValueOrDefault(index, null);
        if (infos != null)
        {
            for (int i = 0; i < infos.Count; ++i)
            {
                if (infos[i].gameObject != null)
                {
                    infos[i].gameObject.SetActive(true);
                }
            }
        }
    }

    private void CheckIsNeedLoad()
    {
        if (needToLoadIndexQueue.Count == 0) return;
        int index = needToLoadIndexQueue.Dequeue();
        NeedToLoadIndex(index);
    }

    private void NeedToLoadIndex(int index)
    {
        List<UIDownInfo> infos = uiLoadInfoDic.GetValueOrDefault(index, null);
        if (infos != null)
        {
            for (int i = 0; i < infos.Count; ++i)
            {
                UIDownInfo info = infos[i];
                info.isLoaded = LoadState.Loading;
                info.loadIndex = AssetBundleManager.Instance.LoadAsset<GameObject>(info.bundleName, info.assetName,
                    (GameObject obj) => { AssetBundleLoadCallBack(obj, info, index); });
            }
        }
    }

    private void AssetBundleLoadCallBack(GameObject obj, UIDownInfo info, int targetIndex)
    {
        // 已关闭则忽略异步回调
        if (this.isOpen == false)
        {
            return;
        }

        GameObject instanceObj = GameObject.Instantiate(obj, rootView.transform);
        instanceObj.SetActive(false);
        info.gameObject = instanceObj;
        info.isLoaded = LoadState.Loaded;
        info.loadIndex = -1;

        // 若需可在此显式设 RectTransform、兄弟顺序等
        //instanceObj.transform.SetParent(rootView.transform);
        //instanceObj.transform.SetSiblingIndex(info.order);
        //RectTransform trans = instanceObj.gameObject.GetComponent<RectTransform>();
        //trans.localPosition = Vector3.zero;
        //trans.localScale = Vector3.one;

        // 收集 UINameTable
        UINameTable nameTable = instanceObj.transform.GetComponent<UINameTable>();
        if (nameTable == null)
        {
            Debug.LogWarning("[BaseView] 未挂 UINameTable（可忽略）: " + instanceObj.name);
        }
        else
        {
            nameTableDic.AddUINameTable(nameTable.GetNameTableList());
        }

        ChechIndexIsLoadFinish(targetIndex);
    }

    private void ChechIndexIsLoadFinish(int targetIndex)
    {
        bool isAllLoaded = false;
        List<UIDownInfo> infos = uiLoadInfoDic.GetValueOrDefault(targetIndex, null);
        if (infos != null)
        {
            isAllLoaded = true;
            for (int i = 0; i < infos.Count; ++i)
            {
                UIDownInfo info = infos[i];
                if (info.isLoaded != LoadState.Loaded) isAllLoaded = false;
            }
        }

        // 该 index 下所有子资源已加载完
        if (isAllLoaded == true)
        {
            // index 0 时设置 transform 并调 LoadCallBack
            if (targetIndex == 0)
            {
                for (int i = 0; i < infos.Count; ++i)
                {
                    UIDownInfo info = infos[i];
                    info.gameObject.SetActive(true);
                }
                if (infos.Count > 0)
                {
                    transform = infos[0].gameObject.transform;
                }
                this.LoadCallBack();
            }
            this.LoadIndexCallBack(targetIndex);
            this.FlushShowView(targetIndex);

            this.isLoadedDic[targetIndex] = LoadState.Loaded;
            this.CheckIsNeedLoad();
        }
    }

    private void FlushShowView(int index)
    {
        if (CurShowIndex == index)
        {
            TryFlushViewShow(index, true);
            this.ShowIndexCallBack(index);
        }
        this.Flush();
    }

    #endregion
}
