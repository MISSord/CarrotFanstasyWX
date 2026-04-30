using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MapBigLevelPanel : BaseView
    {

        private GridLayoutGroup gridLayout;
        private SlideScrollView slideScroll;

        private int bigLevelPageCount;
        private Transform[] bigLevelPage;
        private Transform bigLevelContentTrans;

        private bool hasRigisterEvent;
        private int curBigLevel = 1;

        public override void InitData()
        {
            viewName = "MapBigLevelPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MapViewPrefab, "MapBigLevelPanel");
        }

        protected override void LoadCallBack()
        {
            this.bigLevelPageCount = MapServer.Instance.mapModel.GetBigLevelCount();
            this.bigLevelPage = new Transform[bigLevelPageCount];
            this.hasRigisterEvent = false;

            this.gridLayout = this.nameTableDic["content"].GetComponent<GridLayoutGroup>();

            this.bigLevelContentTrans = this.nameTableDic["content"].transform;

            this.InitGridLayoutAndSroll();
            this.LoadBigLevelInfo();

            this.AddListener();
        }

        private void AddListener()
        {
            XUI.AddButtonListener(this.nameTableDic["btn_last_page"].GetComponent<Button>(), this.ToTheLastLevelPage);
            XUI.AddButtonListener(this.nameTableDic["btn_next_page"].GetComponent<Button>(), this.ToTheNextLevelPage);

            XUI.AddButtonListener(this.nameTableDic["btn_return"].GetComponent<Button>(), this.ReturnToMainPanel);
            XUI.AddButtonListener(this.nameTableDic["btn_help"].GetComponent<Button>(), this.ShowHelpPanel);
            MapServer.Instance.eventDispatcher.AddListener(MapEventType.MAP_INFO_CHANGE, this.UpdateBigLevelInfo);
        }

        private void RemoveListener()
        {
            MapServer.Instance.eventDispatcher.RemoveListener(MapEventType.MAP_INFO_CHANGE, this.UpdateBigLevelInfo);
        }

        private void InitGridLayoutAndSroll()
        {
            float sizeChange = 1f;
            float newCellX = GameConfig.BIG_LEVEL_UNIT_SIZE_X * sizeChange;
            float newCellY = GameConfig.BIG_LEVEL_UNIT_SIZE_Y * sizeChange;
            this.gridLayout.cellSize = new Vector2(newCellX, newCellY);
            float newSpacing = GameConfig.BIG_LEVEL_UNIT_SPACING_X * sizeChange;
            this.gridLayout.spacing = new Vector2(newSpacing, 0);
            this.slideScroll = new SlideScrollView();
            this.slideScroll.LoadSrollView(this.nameTableDic["scroller"].transform, (int)newCellX, (int)newSpacing);
            this.slideScroll.SetContentLength(this.bigLevelPageCount);
        }

        private void LoadBigLevelInfo()
        {
            for (int i = 0; i <= bigLevelPageCount - 1; i++)
            {
                bigLevelPage[i] = bigLevelContentTrans.GetChild(i);
                this.ShowBigLevelState(MapServer.Instance.mapModel.GetBigLevelInfo(i + 1), bigLevelPage[i], i + 1);
            }
            hasRigisterEvent = true;
        }

        public void ShowBigLevelState(BigLevelInfo info, Transform theBigLevelButtonTrans, int bigLevelID)
        {
            if (info.isLock == false)
            {
                theBigLevelButtonTrans.Find("img_lock").gameObject.SetActive(false);
                theBigLevelButtonTrans.Find("img_page").gameObject.SetActive(true);
                theBigLevelButtonTrans.Find("img_page").Find("txt_page").GetComponent<Text>().text
                    = info.unlockCount.ToString() + "/" + info.count.ToString();
                Button theBigLevelButtonCom = theBigLevelButtonTrans.GetComponent<Button>();
                theBigLevelButtonCom.interactable = true;
                theBigLevelButtonCom.onClick.RemoveAllListeners();
                XUI.AddButtonListener(theBigLevelButtonCom, () =>
                {
                    //UIViewService.OpenMapNormalLevelPanel(info.bigLevel);

                });
            }
            else
            {
                theBigLevelButtonTrans.Find("img_lock").gameObject.SetActive(true);
                theBigLevelButtonTrans.Find("img_page").gameObject.SetActive(false);
                theBigLevelButtonTrans.GetComponent<Button>().interactable = false;
            }
        }

        public void UpdateBigLevelInfo()
        {
            for (int i = 0; i <= bigLevelPageCount - 1; i++)
            {
                BigLevelInfo info = MapServer.Instance.mapModel.GetBigLevelInfo(i + 1);
                this.ShowBigLevelState(info, bigLevelPage[i], i + 1);
            }
        }

        private void ReturnToMainPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            this.Close();
        }

        private void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
            UIServer.Instance.PlayButtonEffect();
        }

        private void ToTheNextLevelPage()
        {
            if (this.curBigLevel >= this.bigLevelPageCount)
            {
                return;
            }
            this.curBigLevel++;
            this.slideScroll.ToNextPage();
            UIServer.Instance.PlayPagingEffect();
        }

        private void ToTheLastLevelPage()
        {
            if (this.curBigLevel <= 1)
            {
                return;
            }
            this.curBigLevel--;
            this.slideScroll.ToLastPage();
            UIServer.Instance.PlayPagingEffect();
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
        }
    }
}
