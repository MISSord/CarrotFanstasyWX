using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapNormalLevelPanel : BasePanel
    {
        private SingleMapInfo[] levelInfoList;

        private string filePath;//图片资源加载的根路径
        public int currentBigLevelID;
        public int currentLevelID;
        private string theSpritePath;

        private int towerCount = 5;

        private Transform levelContentTrans;
        private GameObject nodeLockBtn;//未解锁关卡遮挡板
        private Transform nodeTowerTrans;//建塔列表
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

        private List<GameObject> levelContentImageGos;//实例化出来的地图卡片UI
        private List<GameObject> towerContentImageGos;//实例化出来的建塔列表UI

        public MapNormalLevelPanel(Dictionary<string, dynamic> param) : base(param)
        {
            this.prefabUrl = "Prefabs/Business/Map/MapLevelPanel";
        }

        public override void Init()
        {
            this.panelManagerUnit.registerOnAssetReady(this.OnAssetReady);
            this.panelManagerUnit.registerOnDestroy(this.OnDestroy);
            this.levelInfoList = MapServer.Instance.mapModel.getOnceBigLevelMapInfo(currentBigLevelID);
            this.filePath = "Pictures/GameOption/Normal/Level/";

            this.levelContentImageGos = new List<GameObject>();
            this.towerContentImageGos = new List<GameObject>();

            this.currentLevelID = 1;
        }

        protected override void OnAssetReady()
        {
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

        //更新地图UI的方法(动态UI)
        public void loadLevelUI()
        {
            this.imgBGLeft.sprite = ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Left");
            this.imgBGRight.sprite = ResourceLoader.Instance.loadRes<Sprite>(this.theSpritePath + "BG_Right");
            for (int i = 0; i < levelInfoList.Length; i++)
            {
                levelContentImageGos.Add(this.CreateUIAndSetUIPosition("Prefabs/UI/node_level", levelContentTrans));
                //更换关卡图片
                String path = theSpritePath + "Level_" + (i + 1).ToString();
                levelContentImageGos[i].transform.GetComponent<Image>().sprite = ResourceLoader.Instance.loadRes<Sprite>(path);
                levelContentImageGos[i].transform.Find("img_carrot").gameObject.SetActive(false);
                levelContentImageGos[i].transform.Find("img_all_clear").gameObject.SetActive(false);
            }
            //设置滚动视图Content的大小
            this.slideScrollView.SetContentLength(levelInfoList.Length);
        }

        private void updateLevelUI()
        {
            for (int i = 0; i < levelInfoList.Length; i++)
            {
                SingleMapInfo info = this.levelInfoList[i];
                if (info.unLocked == MapInfoType.UNLOCK_LEVEL)
                {
                    //已解锁
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

        //更新静态UI
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
        public void startGame()
        {
            MapServer.Instance.sendGameMapInfo(this.currentBigLevelID, this.currentLevelID);
            //MapServer.Instance.sendSetSingleMapInfo(this.levelInfoList[this.currentLevelID - 1]);
            UIServer.Instance.playButtonEffect();
        }

        //实例化UI
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
            UIServer.Instance.playPagingEffect();
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
            UIServer.Instance.playPagingEffect();
        }

        public void showHelpPanel()
        {
            ServerProvision.panelServer.ShowPanel(new HelpPanel(null));
            UIServer.Instance.playButtonEffect();
        }

        private void returnToLastPanel()
        {
            UIServer.Instance.playButtonEffect();
            this.Finish();
        }

        private void AddListener()
        {
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, this.updateMapInfo);
            this.btnStartGame.onClick.AddListener(this.startGame);
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

        //销毁地图卡
        private void destroyLevelUI()
        {
            if (levelContentImageGos.Count > 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject.Destroy(levelContentImageGos[i]);
                }
                slideScrollView.InitScrollLength();
                levelContentImageGos.Clear();
            }
        }

        protected override void OnDestroy()
        {
            this.destroyLevelUI();
            this.RemoveListener();
            base.OnDestroy();
        }
    }
}
