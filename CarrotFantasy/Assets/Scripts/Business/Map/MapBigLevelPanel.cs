using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapBigLevelPanel : BasePanel
    {
        private Button btnReturn;
        private Button btnHelp;

        private Button btnNext;
        private Button btnLast;

        private GridLayoutGroup gridLayout;
        private SlideScrollView slideScroll;

        private int bigLevelPageCount;//大关卡总数
        private Transform[] bigLevelPage;//大关卡按钮数组
        private Transform bigLevelContentTrans;//滚动视图的content

        private bool hasRigisterEvent;
        private int curBigLevel;

        public MapBigLevelPanel(Dictionary<string, dynamic> param) : base(param)
        {
            this.prefabUrl = "Prefabs/Business/Map/MapBigLevelPanel";
            this.curBigLevel = 1;
        }

        public override void Init()
        {
            base.Init();
            this.panelManagerUnit.registerOnAssetReady(this.OnAssetReady);
            this.panelManagerUnit.registerOnDestroy(this.OnDestroy);
            this.bigLevelPageCount = MapServer.Instance.mapModel.getBigLevelCount(); //后期读配置
            this.bigLevelPage = new Transform[bigLevelPageCount];
            this.hasRigisterEvent = false;
        }

        protected override void OnAssetReady()
        {
            this.btnReturn = this.transform.Find("node_up/btn_return").GetComponent<Button>();
            this.btnHelp = this.transform.Find("node_up/btn_help").GetComponent<Button>();

            this.btnNext = this.transform.Find("node_center/btn_next_page").GetComponent<Button>();
            this.btnLast = this.transform.Find("node_center/btn_last_page").GetComponent<Button>();

            this.gridLayout = this.transform.Find("node_center/scroller/viewport/content").GetComponent<GridLayoutGroup>();

            this.bigLevelContentTrans = this.transform.Find("node_center/scroller/viewport/content");

            this.initGridLayoutAndSroll();
            this.loadBigLevelInfo();

            this.AddListener();
        }

        private void AddListener()
        {
            this.btnLast.onClick.AddListener(this.toTheLastLevelPage);
            this.btnNext.onClick.AddListener(this.toTheNextLevelPage);

            this.btnReturn.onClick.AddListener(this.returnToMainPanel);
            this.btnHelp.onClick.AddListener(this.showHelpPanel);
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, this.updateBigLevelInfo);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, this.updateBigLevelInfo);
        }

        private void initGridLayoutAndSroll()
        {
            //float curHeight = UIUtil.Instance.curSetScreenSize.y;
            float sizeChange = 1;//curHeight / GameConfig.DEVELOPMENT_SCREEN_SIZE.y;

            float newCellX = GameConfig.BIG_LEVEL_UNIT_SIZE_X * sizeChange;
            float newCellY = GameConfig.BIG_LEVEL_UNIT_SIZE_Y * sizeChange;
            this.gridLayout.cellSize = new Vector2(newCellX, newCellY);

            float newSpacing = GameConfig.BIG_LEVEL_UNIT_SPACING_X * sizeChange;
            this.gridLayout.spacing = new Vector2(newSpacing, 0);
            this.slideScroll = new SlideScrollView();
            this.slideScroll.loadSrollView(this.transform.Find("node_center/scroller"), (int)newCellX, (int)newSpacing);
            this.slideScroll.SetContentLength(this.bigLevelPageCount);
        }

        private void loadBigLevelInfo()
        {
            //显示大关卡信息
            for (int i = 0; i <= bigLevelPageCount - 1; i++)
            {
                bigLevelPage[i] = bigLevelContentTrans.GetChild(i);
                this.ShowBigLevelState(MapServer.Instance.mapModel.getBigLevelInfo(i + 1), bigLevelPage[i], i + 1);
            }
            hasRigisterEvent = true;
        }

        public void ShowBigLevelState(BigLevelInfo info, Transform theBigLevelButtonTrans, int bigLevelID)
        {
            if (info.isLock == false)//解锁状态
            {
                theBigLevelButtonTrans.Find("img_lock").gameObject.SetActive(false);
                theBigLevelButtonTrans.Find("img_page").gameObject.SetActive(true);
                theBigLevelButtonTrans.Find("img_page").Find("txt_page").GetComponent<Text>().text
                    = info.unlockCount.ToString() + "/" + info.count.ToString();
                Button theBigLevelButtonCom = theBigLevelButtonTrans.GetComponent<Button>();
                theBigLevelButtonCom.interactable = true;
                theBigLevelButtonCom.onClick.AddListener(() =>
                {
                    //mUIFacade.PlayButtonAudioClip();
                    //进入小关卡
                    MapNormalLevelPanel gameNormalLevelPanel = new MapNormalLevelPanel(null);
                    gameNormalLevelPanel.currentBigLevelID = info.bigLevel;
                    ServerProvision.panelServer.ShowPanel(gameNormalLevelPanel);

                    //this.Finish();
                });

            }
            else//未解锁
            {
                theBigLevelButtonTrans.Find("img_lock").gameObject.SetActive(true);
                theBigLevelButtonTrans.Find("img_page").gameObject.SetActive(false);
                theBigLevelButtonTrans.GetComponent<Button>().interactable = false;
            }
        }

        public void updateBigLevelInfo()
        {
            for (int i = 0; i <= bigLevelPageCount - 1; i++)
            {
                BigLevelInfo info = MapServer.Instance.mapModel.getBigLevelInfo(i + 1);
                this.ShowBigLevelState(info, bigLevelPage[i], i + 1);
            }
        }

        private void returnToMainPanel()
        {
            UIServer.Instance.playButtonEffect();
            this.Finish();
        }

        private void showHelpPanel()
        {
            ServerProvision.panelServer.ShowPanel(new HelpPanel(null));
            UIServer.Instance.playButtonEffect();
        }

        private void toTheNextLevelPage()
        {
            //mUIFacade.PlayButtonAudioClip();
            if (this.curBigLevel >= this.bigLevelPageCount)
            {
                return;
            }
            this.curBigLevel++;
            this.slideScroll.ToNextPage();
            UIServer.Instance.playPagingEffect();
        }

        private void toTheLastLevelPage()
        {
            //mUIFacade.PlayButtonAudioClip();
            if (this.curBigLevel <= 1)
            {
                return;
            }
            this.curBigLevel--;
            this.slideScroll.ToLastPage();
            UIServer.Instance.playPagingEffect();
        }

        protected override void OnDestroy()
        {
            this.RemoveListener();
            base.OnDestroy();
        }
    }
}
