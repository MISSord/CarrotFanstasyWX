using System.Collections.Generic;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>肉鸽大关选单（复用 MapBigLevelPanel 预制体）。</summary>
    public class RoguelikeBigLevelPanel : BaseView
    {
        SelectableScrollerList<BigLevelInfo, BigLevelCellView> scrollerList;

        public override void InitData()
        {
            viewName = "RoguelikeBigLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapBigLevelPanel");
        }

        protected override void LoadCallBack()
        {
            var scrollerGo = nameTableDic["scroller"];
            scrollerList = new SelectableScrollerList<BigLevelInfo, BigLevelCellView>(scrollerGo, defaultSelectIndex: 0);
            scrollerList.SetOnSelected(OnBigLevelSelected);
            AddListener();
        }

        protected override void ShowIndexCallBack(int viewIndex)
        {
            RefreshBigLevelList();
            scrollerList.SetSelectIndex(0, invokeCallback: false);
        }

        void RefreshBigLevelList()
        {
            var model = RoguelikeMapServer.Instance.mapModel;
            int count = model.GetBigLevelCount();
            var items = new List<BigLevelInfo>(count);
            for (int i = 1; i <= count; i++)
            {
                RoguelikeBigLevelInfo src = model.GetBigLevelInfo(i);
                if (src == null)
                {
                    continue;
                }

                items.Add(new BigLevelInfo
                {
                    bigLevel = src.bigLevel,
                    count = src.count,
                    unlockCount = src.unlockCount,
                    isLock = src.isLock,
                });
            }

            scrollerList.SetItemsList(items);
        }

        void OnBigLevelSelected(int index, BigLevelInfo info)
        {
            if (info == null)
            {
                return;
            }

            if (info.isLock)
            {
                UIServer.Instance.ShowTip("该大关尚未解锁");
                return;
            }

            OpenNormalLevelPanel(info.bigLevel);
        }

        void OpenNormalLevelPanel(int bigLevelId)
        {
            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(RoguelikeNormalLevelPanel), out BaseView view))
            {
                return;
            }

            var panel = (RoguelikeNormalLevelPanel)view;
            panel.OpenForBigLevel(bigLevelId);
            ViewManager.Instance.OpenView<RoguelikeNormalLevelPanel>();
        }

        void AddListener()
        {
            XUI.AddButtonListener(nameTableDic["btn_last_page"].GetComponent<Button>(), ToTheLastLevelPage);
            XUI.AddButtonListener(nameTableDic["btn_next_page"].GetComponent<Button>(), ToTheNextLevelPage);
            XUI.AddButtonListener(nameTableDic["btn_help"].GetComponent<Button>(), ShowHelpPanel);
            RoguelikeMapServer.Instance.eventDispatcher.AddListener(
                RoguelikeMapEventType.MAP_INFO_CHANGE,
                UpdateBigLevelInfo);
        }

        void RemoveListener()
        {
            if (RoguelikeMapServer.Instance != null)
            {
                RoguelikeMapServer.Instance.eventDispatcher.RemoveListener(
                    RoguelikeMapEventType.MAP_INFO_CHANGE,
                    UpdateBigLevelInfo);
            }
        }

        void UpdateBigLevelInfo()
        {
            int selected = this.scrollerList.SelectedIndex;
            this.RefreshBigLevelList();

            if (selected >= 0 && selected < this.scrollerList.Items.Count)
            {
                this.scrollerList.SetSelectIndex(selected, invokeCallback: false, refreshVisible: true);
            }
            else
            {
                this.scrollerList.Reload();
            }
        }

        void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
        }

        void ToTheNextLevelPage()
        {
            int next = scrollerList.SelectedIndex + 1;
            if (next >= scrollerList.Items.Count)
            {
                return;
            }

            scrollerList.JumpTo(next, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: false);
        }

        void ToTheLastLevelPage()
        {
            int prev = scrollerList.SelectedIndex - 1;
            if (prev < 0)
            {
                return;
            }

            scrollerList.JumpTo(prev, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: false);
        }

        protected override void ReleaseCallBack()
        {
            RemoveListener();
            scrollerList = null;
        }
    }
}
