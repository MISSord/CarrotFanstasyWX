using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapBigLevelPanel : BaseView
    {
        private SelectableScrollerList<BigLevelInfo> scrollerList;

        public override void InitData()
        {
            viewName = "MapBigLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapBigLevelPanel");
        }

        protected override void LoadCallBack()
        {
            var scrollerGo = nameTableDic["scroller"];
            scrollerList = new SelectableScrollerList<BigLevelInfo>(scrollerGo, defaultSelectIndex: 0);
            scrollerList.SetOnSelected(OnBigLevelSelected);

            AddListener();
        }

        protected override void ShowIndexCallBack(int viewIndex)
        {
            RefreshBigLevelList();
            scrollerList.SetSelectIndex(0, invokeCallback: false);
        }

        private void RefreshBigLevelList()
        {
            var model = MapServer.Instance.mapModel;
            int count = model.GetBigLevelCount();
            var items = new List<BigLevelInfo>(count);
            for (int i = 1; i <= count; i++)
            {
                items.Add(model.GetBigLevelInfo(i));
            }

            scrollerList.SetItemsList(items);
        }

        private void OnBigLevelSelected(int index, BigLevelInfo info)
        {
            if (info == null || info.isLock)
            {
                return;
            }

            OpenNormalLevelPanel(info.bigLevel);
        }

        private void OpenNormalLevelPanel(int bigLevelId)
        {
            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(MapNormalLevelPanel), out BaseView view))
            {
                return;
            }

            var panel = (MapNormalLevelPanel)view;
            panel.SetBigLevel(bigLevelId);
            MapServer.Instance.SendGameMapInfo(bigLevelId, 0);
            ViewManager.Instance.OpenView<MapNormalLevelPanel>();
            UIServer.Instance.PlayButtonEffect();
        }

        private void AddListener()
        {
            XUI.AddButtonListener(nameTableDic["btn_last_page"].GetComponent<Button>(), ToTheLastLevelPage);
            XUI.AddButtonListener(nameTableDic["btn_next_page"].GetComponent<Button>(), ToTheNextLevelPage);
            XUI.AddButtonListener(nameTableDic["btn_return"].GetComponent<Button>(), ReturnToMainPanel);
            XUI.AddButtonListener(nameTableDic["btn_help"].GetComponent<Button>(), ShowHelpPanel);
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, UpdateBigLevelInfo);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, UpdateBigLevelInfo);
        }

        public void UpdateBigLevelInfo()
        {
            int selected = scrollerList.SelectedIndex;
            RefreshBigLevelList();

            if (selected >= 0 && selected < scrollerList.Items.Count)
            {
                scrollerList.SetSelectIndex(selected, invokeCallback: false);
            }
            else
            {
                scrollerList.RefreshVisible();
            }
        }

        private void ReturnToMainPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
        }

        private void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
            UIServer.Instance.PlayButtonEffect();
        }

        private void ToTheNextLevelPage()
        {
            int next = scrollerList.SelectedIndex + 1;
            if (next >= scrollerList.Items.Count)
            {
                return;
            }

            scrollerList.JumpTo(next, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: false);
            UIServer.Instance.PlayPagingEffect();
        }

        private void ToTheLastLevelPage()
        {
            int prev = scrollerList.SelectedIndex - 1;
            if (prev < 0)
            {
                return;
            }

            scrollerList.JumpTo(prev, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: false);
            UIServer.Instance.PlayPagingEffect();
        }

        protected override void ReleaseCallBack()
        {
            RemoveListener();
            scrollerList = null;
        }
    }
}
