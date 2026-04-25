using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapNormalLevelPanel : BaseView
    {
        private static MapNormalLevelPanel _instance;
        public static MapNormalLevelPanel Instance => _instance ?? (_instance = new MapNormalLevelPanel());

        private MapNormalLevelPanel() { }

        private SingleMapInfo[] levelInfoList;
        private string filePath;
        public int currentBigLevelID;
        public int currentLevelID;
        private string theSpritePath;
        private int towerCount = 5;

        private Transform levelContentTrans;
        private GameObject nodeLockBtn;
        private Transform nodeTowerTrans;
        private Image imgBGLeft;
        private Image imgBGRight;
        private Text txtTotalWaves;
        private Transform scroller;
        private SlideScrollView slideScrollView;

        private Button btnStartGame;
        private Button btnNextLevel;
        private Button btnLastLevel;
        private Button btnReturn;
        private Button btnHelp;

        private List<GameObject> levelContentImageGos;
        private List<GameObject> towerContentImageGos;

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
            destroyLevelUI();
            this.filePath = "Pictures/GameOption/Normal/Level/";
            this.levelInfoList = MapServer.Instance.mapModel.getOnceBigLevelMapInfo(currentBigLevelID);
            this.levelContentImageGos = new List<GameObject>();
            this.towerContentImageGos = new List<GameObject>();
            this.currentLevelID = 1;

            this.btnReturn = this.transform.Find("node_up/btn_return").GetComponent<Button>();
            this.btnHelp = this.transform.Find("node_up/btn_help").GetComponent<Button>();

            this.levelContentTrans = transform.Find("node_center/scroller/viewport/content");
            this.nodeLockBtn = transform.Find("node_bottom/img_lock_btn").gameObject;
            this.nodeTowerTrans = transform.Find("node_center/node_tower");
            this.imgBGLeft = transform.Find("node_bottom/img_bg_left").GetComponent<Image>();
            this.imgBGRight = transform.Find("node_bottom/img_bg_right").GetComponent<Image>();
            this.txtTotalWaves = transform.Find("node_center/node_total_waves/txt_waves").GetComponent<Text>();
            this.scroller = transform.Find("node_center/scroller");

            this.btnStartGame = this.transform.Find("node_bottom/btn_start").GetComponent<Button>();
            this.btnLastLevel = this.transform.Find("node_center/btn_last_page").GetComponent<Button>();
            this.btnNextLevel = this.transform.Find("node_center/btn_next_page").GetComponent<Button>();

            this.theSpritePath = filePath + currentBigLevelID.ToString() + "/";

            this.slideScrollView = new SlideScrollView();
            this.slideScrollView.loadSrollView(this.scroller, 1100, 300);

            this.loadLevelUI();
            this.updateLevelUI();
            this.updateTowerUI();

            slideScrollView.Init();

            this.AddListener();
        }

        public void loadLevelUI()
        {
            this.imgBGLeft.sprite = ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Left");
            this.imgBGRight.sprite = ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Right");
            for (int i = 0; i < levelInfoList.Length; i++)
            {
                levelContentImageGos.Add(this.CreateUIAndSetUIPosition("Prefabs/UI/node_level", levelContentTrans));
                String path = theSpritePath + "Level_" + (i + 1).ToString();
                levelContentImageGos[i].transform.GetComponent<Image>().sprite = ResourceLoader.Instance.loadRes<Sprite>(path);
                levelContentImageGos[i].transform.Find("img_carrot").gameObject.SetActive(false);
                levelContentImageGos[i].transform.Find("img_all_clear").gameObject.SetActive(false);
            }
            this.slideScrollView.SetContentLength(levelInfoList.Length);
        }

        private void updateLevelUI()
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

        public void updateTowerUI()
        {
            if (towerContentImageGos.Count == 0)
            {
                for (int i = 0; i < this.towerCount; i++)
                {
                    towerContentImageGos.Add(CreateUIAndSetUIPosition("Prefabs/UI/node_tower", this.nodeTowerTrans));
                }
            }

            Stage stage = MapServer.Instance.mapModel.getStage(this.currentBigLevelID, this.currentLevelID);
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
            MapServer.Instance.sendGameMapInfo(this.currentBigLevelID, this.currentLevelID);
            UIServer.Instance.PlayButtonEffect();
        }

        public GameObject CreateUIAndSetUIPosition(string uiName, Transform parentTrans)
        {
            GameObject itemGo = GameObject.Instantiate(ResourceLoader.Instance.getGameObject(uiName));
            itemGo.transform.SetParent(parentTrans, false);
            itemGo.transform.localPosition = Vector3.zero;
            itemGo.transform.localScale = Vector3.one;
            return itemGo;
        }

        public void toNextLevel()
        {
            if (this.currentLevelID >= this.levelInfoList.Length)
            {
                return;
            }
            currentLevelID++;
            this.slideScrollView.ToNextPage();
            this.updateTowerUI();
            UIServer.Instance.PlayPagingEffect();
        }

        public void toLastLevel()
        {
            if (currentLevelID <= 1)
            {
                return;
            }
            currentLevelID--;
            this.slideScrollView.ToLastPage();
            this.updateTowerUI();
            UIServer.Instance.PlayPagingEffect();
        }

        public void showHelpPanel()
        {
            UIViewService.OpenHelpPanel();
            UIServer.Instance.PlayButtonEffect();
        }

        private void returnToLastPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            this.Close();
        }

        private void AddListener()
        {
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, this.updateMapInfo);
            this.btnStartGame.onClick.AddListener(this.StartGame);
            this.btnLastLevel.onClick.AddListener(this.toLastLevel);
            this.btnNextLevel.onClick.AddListener(this.toNextLevel);
            this.btnReturn.onClick.AddListener(this.returnToLastPanel);
            this.btnHelp.onClick.AddListener(this.showHelpPanel);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, this.updateMapInfo);
        }

        private void updateMapInfo()
        {
            this.levelInfoList = MapServer.Instance.mapModel.getOnceBigLevelMapInfo(this.currentBigLevelID);
            this.updateLevelUI();
        }

        private void destroyLevelUI()
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
            this.destroyLevelUI();
            this.RemoveListener();
        }
    }
}
