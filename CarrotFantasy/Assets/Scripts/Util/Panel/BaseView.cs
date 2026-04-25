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

    // 下面部分子类一般不需要直接访问
    private GameObject rootObject; // 根节点
    private Transform rootView; // 界面挂点
    private Canvas rootCanvas; // 根上的 Canvas
    private int layerOrder = 0;
    private bool isInitData = false;
    private bool isLoadRoot = false;
    private string delayReleaseId;

    private Dictionary<int, List<UIDownInfo>> uiLoadInfoDic = new Dictionary<int, List<UIDownInfo>>();
    private Dictionary<int, LoadState> isLoadedDic = new Dictionary<int, LoadState>(); // 该 index 下资源是否已加载完
    private Dictionary<int, bool> isFirstOpenDic = new Dictionary<int, bool>(); // 是否第一次真正打开该 index
    private Queue<int> needToLoadIndexQueue = new Queue<int>();
    private Dictionary<int, Dictionary<string, string>> flushInfo = new Dictionary<int, Dictionary<string, string>>();

    private bool isPausedByViewStack;

    // 初始信息设置，子类必须实现
    public abstract void InitData();

    public void RegisterData()
    {
        if (isInitData == false)
        {
            nameTableDic = new UINameTableDic();
            this.InitData();
            ViewManager.Instance.RegisterView(this);
        }
    }

    public void DeleteMe()
    {
        nameTableDic.ClearAllInfo();
        needToLoadIndexQueue.Clear();
    }

    public void Release()
    {
        ReleaseCallBack();

        // 先清空实例上的引用
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

        // 卸载 AB 或取消尚未完成的加载
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

        // 清空数据
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
        // 已加载完成则只刷新
        if (CurShowIndex == targetIndex && isLoadedDic[CurShowIndex] == LoadState.Loaded)
        {
            Flush();
            return;
        }

        // 切走前：隐藏旧页
        if (CurShowIndex != 0 && CurShowIndex != -1 && isLoadedDic[CurShowIndex] == LoadState.Loaded)
        {
            TryFlushViewShow(CurShowIndex, false);
        }

        CurShowIndex = targetIndex;

        // 新 index 已就绪则直接显示
        LoadState state = isLoadedDic.GetValueOrDefault(CurShowIndex, LoadState.None);
        if (state == LoadState.Loaded)
        {
            FlushShowView(targetIndex);
            return;
        }
        else
        {
            state = isLoadedDic.GetValueOrDefault(0, LoadState.None);
            // 避免重复入队
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

    #region 回调与可重写
    protected virtual void LoadCallBack() { }

    protected virtual void LoadIndexCallBack(int viewIndex) { }

    protected virtual void ShowIndexCallBack(int viewIndex) { }

    protected virtual void ReleaseCallBack() { }

    protected virtual void CloseCallBack() { }

    protected virtual void OnFlush(int index, Dictionary<string, string> info = null) { }

    protected virtual void OpenCallBack(int index) { }

    /// <summary> 同栈中更靠上的 View 遮挡时由 ViewManager 通知（全序仅栈顶不暂停） </summary>
    protected virtual void OnPause() { }

    /// <summary> 再次成为栈顶，或关闭本界面且曾经栈暂停时调用 </summary>
    protected virtual void OnResume() { }

    /// <summary> 与 FlushViewOrder 同序，仅最末项（sort 最高）为 true </summary>
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

    #region 对外方法

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
        // 第一次打开，使用子类 defaultIndex
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
            // 再次打开：移到同层打开列表尾部，重算叠放顺序
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

        // 延迟释放实例
        if (delayReleaseId != null)
        {
            Debug.LogError($"严重：关闭时延迟释放的计时未清空，需排查: {delayReleaseId}");
            return;
        }

        string time = Time.unscaledTime.ToString();
        delayReleaseId = viewName + time;
        TimeUtility.Instance.SetTimeout(5f, this.Release, false, delayReleaseId);
    }

    #endregion

    #region 内部实现

    // 从 BaseView 预制体建根节点与挂点
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

        // 子界面预制体挂到名为 Root 的节点下
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
        // 已关闭则丢弃（界面已关或正销毁）
        if (this.isOpen == false)
        {
            return;
        }

        GameObject instanceObj = GameObject.Instantiate(obj, rootView.transform);
        instanceObj.SetActive(false);
        info.gameObject = instanceObj;
        info.isLoaded = LoadState.Loaded;
        info.loadIndex = -1;

        // 若需手调父节点、兄弟序、Rect，可取消注释
        //instanceObj.transform.SetParent(rootView.transform);
        //instanceObj.transform.SetSiblingIndex(info.order);
        //RectTransform trans = instanceObj.gameObject.GetComponent<RectTransform>();
        //trans.localPosition = Vector3.zero;
        //trans.localScale = Vector3.one;

        // 通过 UINameTable 注册控件
        UINameTable nameTable = instanceObj.transform.GetComponent<UINameTable>();
        if (nameTable == null)
        {
            Debug.LogError($"该预制体未挂 UINameTable，请检查: {instanceObj.name}");
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

        // 该 index 下各资源都已加载完
        if (isAllLoaded == true)
        {
            // 默认 index 0：全部激活并走 LoadCallBack
            if (targetIndex == 0)
            {
                for (int i = 0; i < infos.Count; ++i)
                {
                    UIDownInfo info = infos[i];
                    info.gameObject.SetActive(true);
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
