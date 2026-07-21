using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>肉鸽小关选单（复用 MapLevelPanel 预制体）。开始 → <see cref="RoguelikeMapServer.EnterLevel"/>。</summary>
    public class RoguelikeNormalLevelPanel : BaseView
    {
        RoguelikeSingleLevelInfo[] levelInfoList;
        public int currentBigLevelID;
        public int currentLevelID = 1;

        SelectableScrollerList<NormalLevelListItem, NormalLevelCellView> scrollerList;
        GameObject nodeLockBtn;
        Text txtTotalWaves;

        public void OpenForBigLevel(int bigLevelId, int levelId = 1)
        {
            this.currentBigLevelID = bigLevelId;
            this.currentLevelID = levelId > 0 ? levelId : 1;
            this.RefreshForCurrentBigLevel();
        }

        public void RestoreSelection(int bigLevelId, int levelId)
        {
            this.OpenForBigLevel(bigLevelId, levelId);
        }

        public override void InitData()
        {
            viewName = "RoguelikeNormalLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapLevelPanel");
        }

        protected override void ReleaseCallBack()
        {
            scrollerList = null;
            RemoveListener();
        }

        protected override void LoadCallBack()
        {
            nodeLockBtn = nameTableDic["img_lock_btn"];
            txtTotalWaves = nameTableDic["txt_waves"].GetComponent<Text>();

            // 肉鸽暂不展示经典塔位，隐藏节点避免空模板。
            GameObject towerNode = nameTableDic.GetGameObjectSafely("node_tower");
            if (towerNode != null)
            {
                towerNode.SetActive(false);
            }

            var scrollerGo = nameTableDic["scroller"];
            scrollerList = new SelectableScrollerList<NormalLevelListItem, NormalLevelCellView>(scrollerGo);
            scrollerList.SetOnSelected(OnLevelSelected);
            AddListener();
        }

        protected override void ShowIndexCallBack(int viewIndex)
        {
            if (this.scrollerList == null)
            {
                return;
            }

            this.RefreshForCurrentBigLevel();
        }

        void RefreshForCurrentBigLevel()
        {
            if (this.scrollerList == null)
            {
                return;
            }

            this.ApplyChapterBackgrounds();
            this.RefreshPanelData();

            int selectIndex = this.currentLevelID - 1;
            if (selectIndex >= 0 && selectIndex < this.scrollerList.Items.Count)
            {
                this.scrollerList.SetSelectIndex(selectIndex, invokeCallback: false, refreshVisible: true);
            }
            else
            {
                this.scrollerList.Reload();
            }

            this.UpdateDetailUI();
        }

        void ApplyChapterBackgrounds()
        {
            if (this.nameTableDic == null)
            {
                return;
            }

            string asset = string.Format("Level_{0}_BG_Left", this.currentBigLevelID);
            string bundle = ResPath.GetRawImagePath(asset);
            RawImage imgLeft = this.nameTableDic["img_bg_left"].GetComponent<RawImage>();
            imgLeft.SetTexture(bundle, asset);

            asset = string.Format("Level_{0}_BG_Right", this.currentBigLevelID);
            bundle = ResPath.GetRawImagePath(asset);
            RawImage imgRight = this.nameTableDic["img_bg_right"].GetComponent<RawImage>();
            imgRight.SetTexture(bundle, asset);
        }

        void RefreshPanelData()
        {
            levelInfoList = RoguelikeMapServer.Instance.mapModel.GetLevelsForBig(currentBigLevelID);
            RefreshLevelList();
        }

        void RefreshLevelList()
        {
            var items = new List<NormalLevelListItem>();
            if (levelInfoList != null)
            {
                for (int i = 0; i < levelInfoList.Length; i++)
                {
                    if (levelInfoList[i] == null)
                    {
                        continue;
                    }

                    items.Add(new NormalLevelListItem { mapInfo = ToDisplayMapInfo(levelInfoList[i]) });
                }
            }

            scrollerList.SetItemsList(items);
        }

        static SingleMapInfo ToDisplayMapInfo(RoguelikeSingleLevelInfo src)
        {
            return new SingleMapInfo
            {
                bigLevelId = src.bigLevelId,
                levelId = src.levelId,
                unLocked = src.unlocked,
                carrotState = src.cleared == RoguelikeMapInfoType.CLEARED
                    ? MapInfoType.CARROT_STATE_NORMAL
                    : (byte)0,
                isAllClear = MapInfoType.NOT_ALL_CLEAR,
            };
        }

        void OnLevelSelected(int index, NormalLevelListItem item)
        {
            if (index < 0 || item?.mapInfo == null)
            {
                return;
            }

            currentLevelID = item.mapInfo.levelId;
            UpdateDetailUI();
        }

        void UpdateDetailUI()
        {
            if (levelInfoList == null || currentLevelID <= 0 || currentLevelID > levelInfoList.Length)
            {
                return;
            }

            RoguelikeSingleLevelInfo info = levelInfoList[currentLevelID - 1];
            if (info == null)
            {
                return;
            }

            if (nodeLockBtn != null)
            {
                nodeLockBtn.SetActive(info.unlocked != RoguelikeMapInfoType.UNLOCK_LEVEL);
            }

            RoguelikeLevelDef def = RoguelikeMapServer.Instance.GetLevelDef(currentBigLevelID, currentLevelID);
            if (txtTotalWaves != null)
            {
                if (def != null)
                {
                    txtTotalWaves.text = def.startingGold.ToString();
                }
                else
                {
                    txtTotalWaves.text = "-";
                }
            }
        }

        public void StartGame()
        {
            if (levelInfoList == null || currentLevelID <= 0 || currentLevelID > levelInfoList.Length)
            {
                return;
            }

            RoguelikeSingleLevelInfo info = levelInfoList[currentLevelID - 1];
            if (info == null || info.unlocked != RoguelikeMapInfoType.UNLOCK_LEVEL)
            {
                UIServer.Instance?.ShowTip("关卡尚未解锁");
                return;
            }

            RoguelikeMapServer.Instance.EnterLevel(currentBigLevelID, currentLevelID);
        }

        public void ToNextLevel()
        {
            int next = scrollerList.SelectedIndex + 1;
            if (next >= scrollerList.Items.Count)
            {
                return;
            }

            scrollerList.JumpTo(next, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: true);
        }

        public void ToLastLevel()
        {
            int prev = scrollerList.SelectedIndex - 1;
            if (prev < 0)
            {
                return;
            }

            scrollerList.JumpTo(prev, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: true);
        }

        void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
        }

        void AddListener()
        {
            RoguelikeMapServer.Instance.eventDispatcher.AddListener(
                RoguelikeMapEventType.MAP_INFO_CHANGE,
                UpdateMapInfo);
            XUI.AddButtonListener(nameTableDic["btn_start"].GetComponent<Button>(), StartGame);
            XUI.AddButtonListener(nameTableDic["btn_last_page"].GetComponent<Button>(), ToLastLevel);
            XUI.AddButtonListener(nameTableDic["btn_next_page"].GetComponent<Button>(), ToNextLevel);
            XUI.AddButtonListener(nameTableDic["btn_help"].GetComponent<Button>(), ShowHelpPanel);
        }

        void RemoveListener()
        {
            if (RoguelikeMapServer.Instance != null)
            {
                RoguelikeMapServer.Instance.eventDispatcher.RemoveListener(
                    RoguelikeMapEventType.MAP_INFO_CHANGE,
                    UpdateMapInfo);
            }
        }

        void UpdateMapInfo()
        {
            if (this.scrollerList == null)
            {
                return;
            }

            int selected = this.scrollerList.SelectedIndex;
            this.RefreshPanelData();
            this.scrollerList.Reload();

            if (selected >= 0 && selected < this.scrollerList.Items.Count)
            {
                this.scrollerList.SetSelectIndex(selected, invokeCallback: false, refreshVisible: true);
                this.currentLevelID = this.scrollerList.Items[selected].mapInfo.levelId;
            }

            this.UpdateDetailUI();
        }
    }
}
