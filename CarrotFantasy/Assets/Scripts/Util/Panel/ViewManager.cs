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
    //界面管理器
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

    private bool isCanShowPanel = true;
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

        //GameRoot.GlobalEventDispatcher.AddListener(SceneEventType.LOAD_SCENE_FINISH, this.SceneLoadFinishCallBack);
    }

    public void RegisterView(BaseView view)
    {
        if (viewDic.ContainsKey(view.ViewName))
        {
            Debug.LogError($"{view.ViewName}界面重复注册");
            return;
        }
        viewDic.Add(view.ViewName, view);
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
    }

    public void RemoveViewFromOpenList(BaseView view)
    {
        UILayer layer = view.Layer;
        List<BaseView> list = viewList[layer];
        list.Remove(view);
        isNeedFlushViewOrder = true;
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
        int sort = 10000;
        for (int i = (int)UILayer.Normal; i <= (int)UILayer.Max; ++i)
        {
            sort = i * layerIntervalOrder + 10000;
            List<BaseView> list = viewList[(UILayer)i];
            if (list.Count >= 15)
                Debug.LogError($"{i}层界面目前达到{list.Count}个了，按道理不应该有这么多个同时打开，查查界面逻辑");
            for (int j = 0; j < list.Count; ++j)
            {
                sort = sort + j * viewIntervalOrder;
                BaseView view = list[j];
                view.ChangeCurCanvaseOrder(sort);
            }
        }
    }


    private void SceneLoadFinishCallBack()
    {
        //this.ReloadPanelLayerInfo();
        //this.TryShowPreLoadPanel();
    }

    //private void TryShowPreLoadPanel()
    //{
    //    for (int i = 0; i <= preLoadPanelList.Count - 1; i++)
    //    {
    //        BasePanel panel = preLoadPanelList[i];
    //        panel.isShowByPreLoad = true;
    //        this.showPanel(panel);
    //    }
    //    preLoadPanelList.Clear();
    //}

    //private void ReloadPanelLayerInfo()
    //{
    //    this.curPanlInfo = Server.sceneServer.getPanelLayerInfo();
    //}

    //public void showPanel(BasePanel targetPanel)
    //{
    //    if (targetPanel == null)
    //    {
    //        Debug.Log("面板类不可用");
    //        return;
    //    }
    //    String curPath = targetPanel.prefabUrl;
    //    if (isCanShowPanel == false)
    //    {
    //        preLoadPanelList.Add(targetPanel);
    //        Debug.Log(String.Format("当前无法打开面板——{0}", curPath));
    //        return;
    //    }
    //    Dictionary<String, System.Object> msg = new Dictionary<string, System.Object>() {
    //            {"panelName",curPath },{ "enableShow", true},{"reason","" } };
    //    this.eventDispatcher.dispatchEvent(PanelEventType.OPEN_PANEL_PREPARE, msg);
    //    if ((bool)msg["enableShow"] == false)
    //    {
    //        Debug.Log(String.Format("{0}面板打开被打断,原因{1}", curPath, msg["reason"]));
    //        return;
    //    }
    //    if (Server.sceneServer.getCurScene() == null)
    //    {
    //        Debug.Log("当前场景不可用");
    //        return;
    //    }
    //    foreach (BasePanel panel in panelList)
    //    {
    //        if (String.Equals(curPath, panel.prefabUrl))
    //        {
    //            Debug.Log(String.Format("已存在当前相同面板——{0}", curPath));
    //            return;
    //        }
    //    }
    //    GameObject item = ResourceLoader.getInstance().getGameObject(curPath);
    //    if (item != null)
    //    {
    //        GameObject tranPanel = GameObject.Instantiate(item);
    //        if (tranPanel == null)
    //        {
    //            Debug.Log(String.Format("打开面板失败,prefab加载失败：{0}", curPath));
    //            return;
    //        }
    //        tranPanel.layer = SceneLayerData.layerType[1]; //UI层
    //        targetPanel.initContainer();
    //        if (curPanlInfo[targetPanel.panelLayerType] == null)
    //        {
    //            Debug.Log(String.Format("当前UI层级不可用——{0}{1}", curPath, targetPanel.panelLayerType));
    //            return;
    //        }
    //        targetPanel.setLayerTran(curPanlInfo[targetPanel.panelLayerType].transform);
    //        targetPanel.initTran(tranPanel.transform);
    //        targetPanel.init(); //子类复写

    //        targetPanel.panelManagerUnit.onAssetReady();

    //        int uid = this.getPanelUid();
    //        targetPanel.panelUid = uid;
    //        panelDic.Add(uid, targetPanel);
    //        panelList.Add(targetPanel);

    //        targetPanel.panelManagerUnit.onResume();
    //    }
    //    else
    //    {
    //        Debug.Log(String.Format("打开面板失败,prefab加载失败：{0}", curPath));
    //    }
    //}

    //public void closePanel(int uid, int closeReason)
    //{
    //    BasePanel targetPanel;
    //    if (panelDic.TryGetValue(uid, out targetPanel))
    //    {
    //        if (targetPanel.isPreLoadOpen == true)
    //        {
    //            this.addToPreLoadPanelList(targetPanel);
    //        }
    //        if (closeReason != PanelCloseReasonType.SCENE_CHANGE)
    //        {
    //            panelList.Remove(targetPanel);
    //        }
    //        panelDic.Remove(uid);
    //        targetPanel.panelManagerUnit.onDestroy();
    //        GameObject.Destroy(targetPanel.container);
    //    }
    //    else
    //    {
    //        Debug.Log(String.Format("关闭面板失败,面板Id：{0}", uid));
    //    }
    //}

    //private void addToPreLoadPanelList(BasePanel panel)
    //{
    //    preLoadPanelList.Add(panel);
    //}

    //public void setShowPanelActive(bool isCanShow)
    //{
    //    this.isCanShowPanel = isCanShow;
    //}

    //public void closeAllPanel(int closeReason, BaseSceneType nextSceneType)
    //{
    //    for (int i = panelList.Count - 1; i >= 0; i--)
    //    {
    //        if (closeReason == PanelCloseReasonType.SCENE_CHANGE && panelList[i].isCloseBySceneChange())
    //        {
    //            this.panelList[i].finish();
    //        }
    //    }
    //}
}
