using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        get { return layer; }
    }

    protected int defaultIndex = 0;
    protected int CurShowIndex = -1;
    protected bool isOpen = false;
    protected UINameTableDic nameTableDic;

    // 根对象：Instantiate 出来的整棵 UI 根
    private GameObject rootObject; // 根 GameObject
    private Transform rootView; // 子页面挂点（如 Root）
    private Canvas rootCanvas; // 根节点上的 Canvas
    private bool isInitData = false;
    private bool isLoadRoot = false;
    private string delayReleaseId;

    /// <summary>index=0 的 UI 加载完成后，首个子物体的 transform，供与 BasePanel 等对接使用</summary>
    protected Transform transform;

    private ViewLoader viewLoader;
    private Dictionary<int, bool> isFirstOpenDic = new Dictionary<int, bool>(); // 各 index 是否已首次打开
    private Dictionary<int, Dictionary<string, string>> flushInfo = new Dictionary<int, Dictionary<string, string>>();

    private bool isPausedByViewStack;

    // 子类实现：如 SetUILoadInfo、数据初始化等
    public abstract void InitData();

    public void RegisterData()
    {
        if (isInitData) return;
        nameTableDic = new UINameTableDic();
        viewLoader = CreateViewLoader();
        this.InitData();
        ViewManager.Instance.RegisterView(this);
        isInitData = true;
    }

    public void DeleteMe()
    {
        nameTableDic.ClearAllInfo();
        viewLoader?.ClearQueue();
    }

    public void Release()
    {
        ReleaseCallBack();
        transform = null;

        viewLoader?.DetachInstances();

        GameObject.Destroy(rootObject);
        rootObject = null;
        rootCanvas = null;
        rootView = null;

        viewLoader?.ReleaseAssets();

        // 清延迟释放标记
        delayReleaseId = null;
        CurShowIndex = -1;
        isFirstOpenDic.Clear();
        nameTableDic.ClearAllInfo();
        isLoadRoot = false;
    }

    protected void SetUILoadInfo(int index, string bundle, string asset)
    {
        viewLoader.RegisterAsset(index, bundle, asset);
    }

    protected void ChangeIndex(int targetIndex)
    {
        // 已处于目标且已加载完成，则只 Flush
        if (CurShowIndex == targetIndex
            && viewLoader.TryGetIndexState(CurShowIndex, out ViewLoadState sameIndexState)
            && sameIndexState == ViewLoadState.Loaded)
        {
            Flush();
            return;
        }

        // 从当前子页切走时，先处理当前页显示/刷新
        if (CurShowIndex != 0 && CurShowIndex != -1
            && viewLoader.TryGetIndexState(CurShowIndex, out ViewLoadState hideState)
            && hideState == ViewLoadState.Loaded)
        {
            viewLoader.SetIndexVisible(CurShowIndex, false);
        }

        CurShowIndex = targetIndex;

        viewLoader.FixStaleLoadingState(CurShowIndex);

        if (viewLoader.IsFullyInstantiated(CurShowIndex))
        {
            viewLoader.MarkIndexLoaded(CurShowIndex);
            TryRunFirstOpenCallBack(targetIndex);
            FlushShowView(targetIndex);
            return;
        }

        viewLoader.RequestLoadRootIfNeeded();
        viewLoader.RequestLoadIndexIfNeeded(targetIndex);

        TryRunFirstOpenCallBack(targetIndex);
        viewLoader.ProcessQueue();
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
    #endregion

    #region 公开接口
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
        return viewLoader.IsIndexLoaded(index);
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

        if (rootObject == null || rootView == null)
        {
            isLoadRoot = false;
            viewLoader.ClearIndexStates();
        }

        if (isLoadRoot == false)
        {
            CreateViewRoot();
        }
        else
        {
            rootView.transform.localPosition = Vector3.zero;
        }

        if (isOpen == true)
        {
            ChangeIndex(index);
            // 已打开则先移出再插回，保证在打开列表尾部（最前显示）
            ViewManager.Instance.RemoveViewFromOpenList(this);
            ViewManager.Instance.AddOpenViewToOpenList(this);
            return;
        }

        ViewManager.Instance.AddOpenViewToOpenList(this);
        // 须在 ChangeIndex 之前置 true：Testing 模式 CACHE_HIT 会同步 ExecuteCallbacks，
        // 否则加载回调见 isOpen=false 会丢弃已加载资源。
        bool wasClosed = !this.isOpen;
        this.isOpen = true;
        ChangeIndex(index);

        // 缓存复开时 CurShowIndex 未变，ChangeIndex 会提前 return，需补一次 ShowIndexCallBack。
        if (wasClosed && viewLoader.IsIndexLoaded(index))
        {
            this.OnReopenIndex(index);
        }
    }

    /// <summary>关闭后再次 Open 且 index 已加载时调用（ChangeIndex 同 index 短路不会触发 ShowIndexCallBack）。</summary>
    protected virtual void OnReopenIndex(int index)
    {
        ShowIndexCallBack(index);
    }

    public virtual void Close()
    {
        // 场景切换等路径会 CloseAllOpenViews；战斗内按钮也可能已 Close 过，重复关闭应静默忽略
        if (!this.isOpen)
        {
            return;
        }

        CloseCallBack();

        this.isOpen = false;
        if (this.isPausedByViewStack)
        {
            this.isPausedByViewStack = false;
            OnResume();
        }
        ViewManager.Instance.RemoveViewFromOpenList(this);
        if (this.rootView != null)
        {
            this.rootView.transform.localPosition = new Vector2(99999, 99999);
        }

        viewLoader?.CancelInFlightLoads();

        if (this.delayReleaseId != null)
        {
            TimeUtility.Instance.RemoveTimeout(this.delayReleaseId);
            this.delayReleaseId = null;
        }

        string time = Time.unscaledTime.ToString();
        this.delayReleaseId = this.viewName + time;
        TimeUtility.Instance.SetTimeout(5f, this.Release, false, this.delayReleaseId);
    }

    /// <summary>取消延迟释放并立即销毁 UI 根节点（离战斗场景时用）。</summary>
    public void CloseAndReleaseNow()
    {
        if (this.delayReleaseId != null)
        {
            TimeUtility.Instance.RemoveTimeout(this.delayReleaseId);
            this.delayReleaseId = null;
        }

        if (this.isOpen)
        {
            CloseCallBack();
            this.isOpen = false;
            if (this.isPausedByViewStack)
            {
                this.isPausedByViewStack = false;
                OnResume();
            }

            ViewManager.Instance?.RemoveViewFromOpenList(this);
        }

        if (this.isLoadRoot)
        {
            this.Release();
        }
    }
    #endregion

    #region 私有方法
    private ViewLoader CreateViewLoader()
    {
        return new ViewLoader(
            () => isOpen,
            () => rootView,
            OnViewLoaderIndexLoadComplete);
    }

    private void OnViewLoaderIndexLoadComplete(int index, IReadOnlyList<GameObject> instances)
    {
        CollectNameTables(index, instances);

        if (index == 0)
        {
            for (int i = 0; i < instances.Count; ++i)
            {
                instances[i].SetActive(true);
            }

            if (instances.Count > 0)
            {
                transform = instances[0].transform;
            }

            LoadCallBack();
            TrySetViewDefaultFunction();
        }

        LoadIndexCallBack(index);
        FlushShowView(index);
    }

    private void CollectNameTables(int index, IReadOnlyList<GameObject> instances)
    {
        if (index == 0)
        {
            nameTableDic.ClearAllInfo();
        }

        for (int i = 0; i < instances.Count; ++i)
        {
            GameObject instance = instances[i];
            if (instance == null)
            {
                continue;
            }

            UINameTable nameTable = instance.GetComponent<UINameTable>();
            if (nameTable == null)
            {
                Debug.LogWarning("[BaseView] 未挂 UINameTable（可忽略）: " + instance.name);
                continue;
            }

            nameTableDic.AddUINameTable(nameTable.GetNameTableList());
        }
    }

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

    private void TryRunFirstOpenCallBack(int targetIndex)
    {
        if (!isFirstOpenDic.GetValueOrDefault(targetIndex, false))
        {
            isFirstOpenDic[targetIndex] = true;
            OpenCallBack(targetIndex);
        }
    }

    private void TrySetViewDefaultFunction()
    {
        // 预设关闭功能
        GameObject btn_close = this.nameTableDic.GetGameObjectSafely("btn_close_window");
        if(btn_close != null)
        {
            Button close = btn_close.transform.GetComponent<Button>();
            if (close != null)
            {
                XUI.AddButtonListener(close, this.Close);
            }
        }
    }

    private void FlushShowView(int index)
    {
        if (CurShowIndex == index)
        {
            viewLoader.SetIndexVisible(index, true);
            this.ShowIndexCallBack(index);
        }
        this.Flush();
    }
    #endregion
}
