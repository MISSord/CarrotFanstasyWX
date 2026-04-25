using System.Collections.Generic;
using UnityEngine;

public enum UILayer
{
    Normal = 1,
    Mid = 2,
    Hight = 3,
    Max = 4,
}

public class ViewManager
{
    private static ViewManager _instance;
    public static ViewManager Instance
    {
        get { return _instance; }
    }

    public ViewManager()
    {
        _instance = this;
    }

    public Dictionary<string, BaseView> viewDic;
    public Dictionary<UILayer, List<BaseView>> viewList;
    public int layerIntervalOrder = 1500;
    public int viewIntervalOrder = 100;

    private List<BaseView> preLoadPanelList = new List<BaseView>();

    //private bool isCanShowPanel = true;
    private bool isNeedFlushViewOrder = false;

    private Camera uiCamera;
    private GameObject uiRoot;
    private GameObject baseView;

    public void DeleteMe()
    {
        viewDic.Clear();
        viewDic = null;

        viewList.Clear();
        viewList = null;
    }

    public void Init()
    {
        viewDic = new Dictionary<string, BaseView>();

        viewList = new Dictionary<UILayer, List<BaseView>>();
        viewList.Add(UILayer.Normal, new List<BaseView>(4));
        viewList.Add(UILayer.Mid, new List<BaseView>(4));
        viewList.Add(UILayer.Hight, new List<BaseView>(4));
        viewList.Add(UILayer.Max, new List<BaseView>(4));

        GameObject camera = GameObject.Find("UICamera");
        uiCamera = camera?.GetComponent<Camera>();

        uiRoot = GameObject.Find("UILayer");

        baseView = Resources.Load<GameObject>("BaseView");

    }

    public void RegisterView(BaseView view)
    {
        if (viewDic.ContainsKey(view.ViewName))
        {
            Debug.LogError("View already registered: " + view.ViewName);
            return;
        }
        viewDic.Add(view.ViewName, view);
    }

    public void UnregisterView(BaseView view)
    {
        if (view == null || viewDic == null) return;
        viewDic.Remove(view.ViewName);
    }

    public GameObject GetBaseViewClone()
    {
        return baseView;
    }

    public Camera GetUICamera()
    {
        return uiCamera;
    }

    public GameObject GetUIRoot()
    {
        return uiRoot;
    }

    public void OpenView(string name)
    {
        if (viewDic.TryGetValue(name, out BaseView view) == false)
        {
            return;
        }
        view.Open();
    }

    public void FlushView(string name, int index, string key, string value)
    {
        if (viewDic.TryGetValue(name, out BaseView view))
        {
            view.TryFlushTargetIndex(index, key, value);
            view.Flush();
        }
    }

    public void AddOpenViewToOpenList(BaseView view)
    {
        UILayer layer = view.Layer;
        List<BaseView> list = viewList[layer];
        list.Add(view);
        isNeedFlushViewOrder = true;
        RefreshViewStackPauseState();
    }

    public void RemoveViewFromOpenList(BaseView view)
    {
        UILayer layer = view.Layer;
        List<BaseView> list = viewList[layer];
        list.Remove(view);
        isNeedFlushViewOrder = true;
        RefreshViewStackPauseState();
    }

    /// <summary> 关闭所有仍处于打开状态（isOpen）的 View，自栈顶向下依次关闭。 </summary>
    public void CloseAllOpenViews()
    {
        if (viewList == null) return;
        var ordered = new List<BaseView>(32);
        for (int i = (int)UILayer.Normal; i <= (int)UILayer.Max; ++i)
        {
            List<BaseView> list = viewList[(UILayer)i];
            for (int j = 0; j < list.Count; ++j)
            {
                BaseView v = list[j];
                if (v != null && v.GetIsOpen()) ordered.Add(v);
            }
        }
        for (int k = ordered.Count - 1; k >= 0; k--)
        {
            ordered[k].Close();
        }
    }

    public void Update()
    {
        if (isNeedFlushViewOrder == true)
        {
            FlushViewOrder();
        }
    }

    private void FlushViewOrder()
    {
        for (int i = (int)UILayer.Normal; i <= (int)UILayer.Max; ++i)
        {
            int baseSort = i * layerIntervalOrder + 10000;
            List<BaseView> list = viewList[(UILayer)i];
            if (list.Count >= 15)
                Debug.LogError($"[ViewManager] UILayer 枚举值 {i} 上已打开的 View 数量 ({list.Count}) 已达 15，排序或叠层可能异常，请检查是否未 Close 或泄漏");
            for (int j = 0; j < list.Count; ++j)
            {
                int sort = baseSort + j * viewIntervalOrder;
                BaseView view = list[j];
                if (view != null)
                {
                    view.ChangeCurCanvaseOrder(sort);
                }
            }
        }
        isNeedFlushViewOrder = false;
    }

    /// <summary> 与 FlushViewOrder 相同的遍历与叠层顺序，仅最后一项为栈顶；非栈顶调用 OnPause，栈顶调用 OnResume。 </summary>
    private void RefreshViewStackPauseState()
    {
        if (viewList == null) return;
        var ordered = new List<BaseView>(16);
        for (int i = (int)UILayer.Normal; i <= (int)UILayer.Max; ++i)
        {
            List<BaseView> list = viewList[(UILayer)i];
            for (int j = 0; j < list.Count; ++j)
            {
                ordered.Add(list[j]);
            }
        }
        if (ordered.Count == 0) return;
        BaseView top = ordered[ordered.Count - 1];
        for (int k = 0; k < ordered.Count; ++k)
        {
            BaseView v = ordered[k];
            v.ApplyViewStackPauseState(v == top);
        }
    }

}
