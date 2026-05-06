using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapNormalLevelPanel : BaseView
    {

        private SingleMapInfo[] levelInfoList;
        private string filePath;
        public int currentBigLevelID;
        public int currentLevelID;
        private string theSpritePath;
        private int towerCount = 5;

        private Transform levelContentTrans;
        private GameObject nodeLockBtn;
        private Transform nodeTowerTrans;
        private Text txtTotalWaves;
        private Transform scroller;
        private SlideScrollView slideScrollView;

        private List<GameObject> levelContentImageGos;
        private List<GameObject> towerContentImageGos;

        private readonly List<AssetLoadHandle> _panelPrefabHandles = new List<AssetLoadHandle>();
        private GameObject _tplNodeLevel;
        private GameObject _tplNodeTower;

        public void SetBigLevel(int bigLevelId)
        {
            this.currentBigLevelID = bigLevelId;
        }

        public override void InitData()
        {
            viewName = "MapNormalLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapLevelPanel");
        }

        protected override void LoadCallBack()
        {
            DestroyLevelUI();
            this.filePath = "Pictures/GameOption/Normal/Level/";
            this.levelInfoList = MapServer.Instance.mapModel.GetOnceBigLevelMapInfo(currentBigLevelID);
            this.levelContentImageGos = new List<GameObject>();
            this.towerContentImageGos = new List<GameObject>();
            this.currentLevelID = 1;

            this.levelContentTrans = this.nameTableDic["content"].transform;
            this.nodeLockBtn = this.nameTableDic["img_lock_btn"];
            this.nodeTowerTrans = this.nameTableDic["node_tower"].transform;
            this.txtTotalWaves = this.nameTableDic["txt_waves"].GetComponent<Text>();
            this.scroller = this.nameTableDic["scroller"].transform;

            this.theSpritePath = filePath + currentBigLevelID.ToString() + "/";

            this.slideScrollView = new SlideScrollView();
            this.slideScrollView.LoadSrollView(this.scroller, 1100, 300);

            this.LoadLevelUI();
            this.UpdateLevelUI();
            this.UpdateTowerUI();

            slideScrollView.Init();

            this.AddListener();
        }

        public void LoadLevelUI()
        {
            this.nameTableDic["img_bg_left"].GetComponent<Image>().sprite =
                ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Left");
            this.nameTableDic["img_bg_right"].GetComponent<Image>().sprite =
                ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Right");
            EnsurePanelPrefabTemplate(ref _tplNodeLevel, UiViewAbPaths.MapViewPrefab, UiViewAbPaths.MapNodeLevelAsset);
            if (_tplNodeLevel == null)
            {
                Debug.LogError("[MapNormalLevelPanel] node_level 预制体加载失败");
                return;
            }

            for (int i = 0; i < levelInfoList.Length; i++)
            {
                levelContentImageGos.Add(InstantiateUiUnderParent(_tplNodeLevel, levelContentTrans));
                String path = theSpritePath + "Level_" + (i + 1).ToString();
                levelContentImageGos[i].transform.GetComponent<Image>().sprite = ResourceLoader.Instance.loadRes<Sprite>(path);
                levelContentImageGos[i].transform.Find("img_carrot").gameObject.SetActive(false);
                levelContentImageGos[i].transform.Find("img_all_clear").gameObject.SetActive(false);
            }
            this.slideScrollView.SetContentLength(levelInfoList.Length);
        }

        private void UpdateLevelUI()
        {
            for (int i = 0; i < levelInfoList.Length; i++)
            {
                SingleMapInfo info = this.levelInfoList[i];
                if (info.unLocked == MapInfoType.UNLOCK_LEVEL)
                {
                    if (info.isAllClear == MapInfoType.ALL_CLEAR)
                    {
                        levelContentImageGos[i].transform.Find("img_all_clear").gameObject.SetActive(true);
                    }
                    if (info.carrotState != 0)
                    {
                        Image carrotImageGo = levelContentImageGos[i].transform.Find("img_carrot").GetComponent<Image>();
                        carrotImageGo.gameObject.SetActive(true);
                        carrotImageGo.sprite = ResourceLoader.Instance.loadRes<Sprite>(filePath + "Carrot_" + info.carrotState);
                    }
                    levelContentImageGos[i].transform.Find("img_lock").gameObject.SetActive(false);
                }
                else
                {
                    levelContentImageGos[i].transform.Find("img_lock").gameObject.SetActive(true);
                }
            }
        }

        public void UpdateTowerUI()
        {
            if (towerContentImageGos.Count == 0)
            {
                EnsurePanelPrefabTemplate(ref _tplNodeTower, UiViewAbPaths.MapViewPrefab, UiViewAbPaths.MapNodeTowerAsset);
                if (_tplNodeTower == null)
                {
                    Debug.LogError("[MapNormalLevelPanel] node_tower 预制体加载失败");
                }
                else
                {
                    for (int i = 0; i < this.towerCount; i++)
                    {
                        towerContentImageGos.Add(InstantiateUiUnderParent(_tplNodeTower, this.nodeTowerTrans));
                    }
                }
            }

            Stage stage = MapServer.Instance.mapModel.GetStage(this.currentBigLevelID, this.currentLevelID);
            SingleMapInfo info = this.levelInfoList[this.currentLevelID - 1];

            if (info.unLocked == MapInfoType.UNLOCK_LEVEL)
            {
                this.nodeLockBtn.SetActive(false);
            }
            else
            {
                this.nodeLockBtn.SetActive(true);
            }
            this.txtTotalWaves.text = stage.mTotalRound.ToString();
            for (int i = 0; i < stage.mTowerIDListLength; i++)
            {
                towerContentImageGos[i].GetComponent<Image>().sprite =
                    ResourceLoader.Instance.loadRes<Sprite>(filePath + "Tower/Tower_" + stage.mTowerIDList[i].ToString());
                towerContentImageGos[i].SetActive(true);
            }
            for (int i = stage.mTowerIDListLength; i < towerContentImageGos.Count; i++)
            {
                towerContentImageGos[i].SetActive(false);
            }
        }

        public void StartGame()
        {
            MapServer.Instance.SendGameMapInfo(this.currentBigLevelID, this.currentLevelID);
            UIServer.Instance.PlayButtonEffect();
        }

        private void EnsurePanelPrefabTemplate(ref GameObject tplField, string bundleName, string assetName)
        {
            if (tplField != null)
            {
                return;
            }

            tplField = GameObjectResourceManager.Instance.LoadPrefabBlocking(bundleName, assetName, out AssetLoadHandle h);
            if (h.IsValid)
            {
                _panelPrefabHandles.Add(h);
            }
        }

        private static GameObject InstantiateUiUnderParent(GameObject tpl, Transform parentTrans)
        {
            GameObject itemGo = GameObject.Instantiate(tpl);
            itemGo.transform.SetParent(parentTrans, false);
            itemGo.transform.localPosition = Vector3.zero;
            itemGo.transform.localScale = Vector3.one;
            return itemGo;
        }

        public void ToNextLevel()
        {
            if (this.currentLevelID >= this.levelInfoList.Length)
            {
                return;
            }
            currentLevelID++;
            this.slideScrollView.ToNextPage();
            this.UpdateTowerUI();
            UIServer.Instance.PlayPagingEffect();
        }

        public void ToLastLevel()
        {
            if (currentLevelID <= 1)
            {
                return;
            }
            currentLevelID--;
            this.slideScrollView.ToLastPage();
            this.UpdateTowerUI();
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
            this.Close();
        }

        private void AddListener()
        {
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, this.UpdateMapInfo);
            XUI.AddButtonListener(this.nameTableDic["btn_start"].GetComponent<Button>(), this.StartGame);
            XUI.AddButtonListener(this.nameTableDic["btn_last_page"].GetComponent<Button>(), this.ToLastLevel);
            XUI.AddButtonListener(this.nameTableDic["btn_next_page"].GetComponent<Button>(), this.ToNextLevel);
            XUI.AddButtonListener(this.nameTableDic["btn_return"].GetComponent<Button>(), this.ReturnToLastPanel);
            XUI.AddButtonListener(this.nameTableDic["btn_help"].GetComponent<Button>(), this.ShowHelpPanel);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, this.UpdateMapInfo);
        }

        private void UpdateMapInfo()
        {
            this.levelInfoList = MapServer.Instance.mapModel.GetOnceBigLevelMapInfo(this.currentBigLevelID);
            this.UpdateLevelUI();
        }

        private void DestroyLevelUI()
        {
            if (levelContentImageGos == null) return;
            if (levelContentImageGos.Count > 0)
            {
                for (int i = 0; i < levelContentImageGos.Count; i++)
                {
                    if (levelContentImageGos[i] != null)
                        GameObject.Destroy(levelContentImageGos[i]);
                }
                if (slideScrollView != null)
                    slideScrollView.InitScrollLength();
                levelContentImageGos.Clear();
            }
        }

        protected override void ReleaseCallBack()
        {
            this.DestroyLevelUI();
            for (int i = 0; i < _panelPrefabHandles.Count; i++)
            {
                _panelPrefabHandles[i].Dispose();
            }

            _panelPrefabHandles.Clear();
            _tplNodeLevel = null;
            _tplNodeTower = null;
            this.RemoveListener();
        }
    }
}
