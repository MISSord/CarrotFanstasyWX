using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapNormalLevelPanel : BaseView
    {
        private const float LevelCellWidth = 1100f;

        private SingleMapInfo[] levelInfoList;
        public int currentBigLevelID;
        public int currentLevelID = 1;
        private int towerCount = 5;

        private SelectableScrollerList<NormalLevelListItem> scrollerList;
        private GameObject nodeLockBtn;
        private Transform nodeTowerTrans;
        private Text txtTotalWaves;

        private List<GameObject> towerContentImageGos;

        private readonly List<AssetLoadHandle> _panelPrefabHandles = new List<AssetLoadHandle>();
        private GameObject _tplNodeTower;
        private bool _isLoadTower = false;

        public void SetBigLevel(int bigLevelId)
        {
            currentBigLevelID = bigLevelId;
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
            scrollerList = new SelectableScrollerList<NormalLevelListItem>(scrollerGo, defaultSelectIndex: 0);
            scrollerList.SetCellSizeGetter(_ => LevelCellWidth);
            scrollerList.SetOnSelected(OnLevelSelected);

            EnsureTowerTemplates();
            AddListener();

            var spritePath = NormalLevelListItem.MapFilePathBase + currentBigLevelID + "/";
            RawImage img_left = nameTableDic["img_bg_left"].GetComponent<RawImage>();
            //img_left.SetTexture();
            //nameTableDic["img_bg_right"].GetComponent<Image>().sprite =
            //    ResourceLoader.Instance.loadRes<Sprite>(spritePath + "BG_Right");
        }

        protected override void ShowIndexCallBack(int viewIndex)
        {
            if (scrollerList == null)
            {
                return;
            }

            RefreshPanelData();

            int selectIndex = currentLevelID - 1;
            if (selectIndex >= 0 && selectIndex < scrollerList.Items.Count)
            {
                scrollerList.SetSelectIndex(selectIndex, invokeCallback: false);
            }

            UpdateTowerUI();
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
                    _isLoadTower = true;
                    UpdateTowerUI();
                }
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
            txtTotalWaves.text = stage.mTotalRound.ToString();

            for (int i = 0; i < stage.mTowerIDListLength; i++)
            {
                towerContentImageGos[i].GetComponent<Image>().sprite =
                    ResourceLoader.Instance.loadRes<Sprite>(
                        NormalLevelListItem.MapFilePathBase + "Tower/Tower_" + stage.mTowerIDList[i]);
                towerContentImageGos[i].SetActive(true);
            }

            for (int i = stage.mTowerIDListLength; i < towerContentImageGos.Count; i++)
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

            MapServer.Instance.SendGameMapInfo(currentBigLevelID, currentLevelID);
            UIServer.Instance.PlayButtonEffect();
        }


        public void ToNextLevel()
        {
            int next = scrollerList.SelectedIndex + 1;
            if (next >= scrollerList.Items.Count)
            {
                return;
            }

            scrollerList.JumpTo(next, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: true);
            UIServer.Instance.PlayPagingEffect();
        }

        public void ToLastLevel()
        {
            int prev = scrollerList.SelectedIndex - 1;
            if (prev < 0)
            {
                return;
            }

            scrollerList.JumpTo(prev, tweenTime: 0.5f, selectOnArrive: true, invokeCallback: true);
            UIServer.Instance.PlayPagingEffect();
        }

        public void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
            UIServer.Instance.PlayButtonEffect();
        }

        private void ReturnToLastPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            Close();
        }

        private void AddListener()
        {
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, UpdateMapInfo);
            XUI.AddButtonListener(nameTableDic["btn_start"].GetComponent<Button>(), StartGame);
            XUI.AddButtonListener(nameTableDic["btn_last_page"].GetComponent<Button>(), ToLastLevel);
            XUI.AddButtonListener(nameTableDic["btn_next_page"].GetComponent<Button>(), ToNextLevel);
            XUI.AddButtonListener(nameTableDic["btn_return"].GetComponent<Button>(), ReturnToLastPanel);
            XUI.AddButtonListener(nameTableDic["btn_help"].GetComponent<Button>(), ShowHelpPanel);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, UpdateMapInfo);
        }

        private void UpdateMapInfo()
        {
            int selected = scrollerList.SelectedIndex;
            RefreshPanelData();

            if (selected >= 0 && selected < scrollerList.Items.Count)
            {
                scrollerList.SetSelectIndex(selected, invokeCallback: false);
                currentLevelID = scrollerList.Items[selected].mapInfo.levelId;
            }

            UpdateTowerUI();
        }


    }
}
