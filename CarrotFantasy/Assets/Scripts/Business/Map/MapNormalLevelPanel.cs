using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapNormalLevelPanel : BaseView
    {
        private SingleMapInfo[] levelInfoList;
        public int currentBigLevelID;
        public int currentLevelID = 1;
        private int towerCount = 5;

        private SelectableScrollerList<NormalLevelListItem, NormalLevelCellView> scrollerList;
        private GameObject nodeLockBtn;
        private Transform nodeTowerTrans;
        private Text txtTotalWaves;

        private List<GameObject> towerContentImageGos;

        private readonly List<AssetLoadHandle> _panelPrefabHandles = new List<AssetLoadHandle>();
        private GameObject _tplNodeTower;
        private bool _isLoadTower = false;


        /// <summary>切换大关并刷新小关列表、背景与详情（已打开时也会刷新）。</summary>
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
            viewName = "MapNormalLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapLevelPanel");
        }

        protected override void ReleaseCallBack()
        {
            if (towerContentImageGos != null && towerContentImageGos.Count > 0)
            {
                for (int i = 0; i < towerContentImageGos.Count; i++)
                {
                    if (towerContentImageGos[i] != null)
                    {
                        GameObject.Destroy(towerContentImageGos[i]);
                    }
                }
                towerContentImageGos.Clear();
            }

            for (int i = 0; i < _panelPrefabHandles.Count; i++)
            {
                _panelPrefabHandles[i].Dispose();
            }
            _panelPrefabHandles.Clear();

            _tplNodeTower = null;
            scrollerList = null;
            _isLoadTower = false;

            RemoveListener();
        }

        protected override void LoadCallBack()
        {
            _isLoadTower = false;

            nodeLockBtn = nameTableDic["img_lock_btn"];
            nodeTowerTrans = nameTableDic["node_tower"].transform;
            txtTotalWaves = nameTableDic["txt_waves"].GetComponent<Text>();

            var scrollerGo = nameTableDic["scroller"];
            scrollerList = new SelectableScrollerList<NormalLevelListItem, NormalLevelCellView>(scrollerGo);
            scrollerList.SetOnSelected(OnLevelSelected);

            EnsureTowerTemplates();
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

            this.UpdateTowerUI();
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

        private void RefreshPanelData()
        {
            levelInfoList = MapServer.Instance.mapModel.GetOnceBigLevelMapInfo(currentBigLevelID);
            RefreshLevelList();
        }

        private void RefreshLevelList()
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

                    items.Add(new NormalLevelListItem { mapInfo = levelInfoList[i] });
                }
            }

            scrollerList.SetItemsList(items);
        }

        private void OnLevelSelected(int index, NormalLevelListItem item)
        {
            if (index < 0 || item?.mapInfo == null)
            {
                return;
            }

            currentLevelID = item.mapInfo.levelId;
            UpdateTowerUI();
        }

        private void EnsureTowerTemplates()
        {
            towerContentImageGos = new List<GameObject>();

            GameObjectResourceManager.Instance.LoadPrefab(UiViewAbPaths.MapViewPrefab, UiViewAbPaths.MapNodeTowerAsset, (GameObject obj) => {
                _tplNodeTower = obj;
                for (int i = 0; i < towerCount; i++)
                {
                    towerContentImageGos.Add(InstantiateUiUnderParent(_tplNodeTower, nodeTowerTrans));
                }

                _isLoadTower = true;
                UpdateTowerUI();
            });
        }

        private GameObject InstantiateUiUnderParent(GameObject tpl, Transform parentTrans)
        {
            GameObject itemGo = GameObject.Instantiate(tpl);
            itemGo.transform.SetParent(parentTrans, false);
            itemGo.transform.localPosition = Vector3.zero;
            itemGo.transform.localScale = Vector3.one;
            return itemGo;
        }

        public void UpdateTowerUI()
        {
            if (_isLoadTower == false || levelInfoList == null || currentLevelID <= 0 || currentLevelID > levelInfoList.Length)
            {
                return;
            }

            Stage stage = MapServer.Instance.mapModel.GetStage(currentBigLevelID, currentLevelID);
            SingleMapInfo info = levelInfoList[currentLevelID - 1];
            if (info == null || stage == null)
            {
                return;
            }

            nodeLockBtn.SetActive(info.unLocked != MapInfoType.UNLOCK_LEVEL);
            txtTotalWaves.text = LevelWaveQuery.GetTotalWaves(currentBigLevelID, currentLevelID).ToString();

            int showCount = Mathf.Min(stage.mTowerIDListLength, towerContentImageGos.Count);
            for (int i = 0; i < showCount; i++)
            {
                Image towerimg = towerContentImageGos[i].GetComponent<Image>();
                string asset = "tower_" + stage.mTowerIDList[i];
                string bundle = ResPath.GetGameOptionImagePath();
                towerimg.SetSprite(bundle, asset);
                towerContentImageGos[i].SetActive(true);
            }

            for (int i = showCount; i < towerContentImageGos.Count; i++)
            {
                towerContentImageGos[i].SetActive(false);
            }
        }

        public void StartGame()
        {
            if (levelInfoList == null || currentLevelID <= 0 || currentLevelID > levelInfoList.Length)
            {
                return;
            }

            SingleMapInfo info = levelInfoList[currentLevelID - 1];
            if (info == null || info.unLocked != MapInfoType.UNLOCK_LEVEL)
            {
                return;
            }

            BattleLauncher.StartClassicLevel(currentBigLevelID, currentLevelID);
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

        public void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
        }

        private void AddListener()
        {
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, UpdateMapInfo);
            XUI.AddButtonListener(nameTableDic["btn_start"].GetComponent<Button>(), StartGame);
            XUI.AddButtonListener(nameTableDic["btn_last_page"].GetComponent<Button>(), ToLastLevel);
            XUI.AddButtonListener(nameTableDic["btn_next_page"].GetComponent<Button>(), ToNextLevel);
            XUI.AddButtonListener(nameTableDic["btn_help"].GetComponent<Button>(), ShowHelpPanel);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, UpdateMapInfo);
        }

        private void UpdateMapInfo()
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

            this.UpdateTowerUI();
        }
    }
}
