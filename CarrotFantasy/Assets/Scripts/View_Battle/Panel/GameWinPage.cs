using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 游戏胜利页面
    /// </summary>
    public class GameWinPage
    {
        private BattleDataComponent dataComponent;

        private Transform transform;

        private Text txtResultShow;
        private Text txtLevelShow;
        private Image img_Carrot;
        public Sprite[] carrotSprites;//0.铜 1.银 2.金

        private Button btnReplay;
        private Button btnChooseLevel;

        public GameWinPage(Transform node)
        {
            this.transform = node;
            this.dataComponent = (BattleDataComponent)GameManager.Instance.baseBattle.getComponent(BattleComponentType.DataComponent);
        }

        public void init()
        {
            this.txtResultShow = this.transform.Find("txt_result_show").GetComponent<Text>();
            this.txtLevelShow = this.transform.Find("txt_level_show").GetComponent<Text>();
            this.img_Carrot = transform.Find("Img_Carrot").GetComponent<Image>();
            carrotSprites = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                carrotSprites[i] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/GameOption/Normal/Level/Carrot_" + (i + 1).ToString());
            }

            this.btnReplay = this.transform.Find("btn_replay").GetComponent<Button>();
            this.btnChooseLevel = this.transform.Find("btn_choose_level").GetComponent<Button>();

            this.AddListener();
        }

        private void AddListener()
        {
            this.btnReplay.onClick.AddListener(this.btnEvenReplay);
            this.btnChooseLevel.onClick.AddListener(this.btnEvenChooseOtherLevel);
            this.dataComponent.eventDispatcher.AddListener(BattleEvent.SHOW_GAME_FINISH_PAGE, this.showGameWinPage);
        }

        private void RemoveListener()
        {
            this.btnReplay.onClick.RemoveAllListeners();
            this.btnChooseLevel.onClick.RemoveAllListeners();
            this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.SHOW_GAME_FINISH_PAGE, this.showGameWinPage);
        }

        public void showGameWinPage()
        {
            this.transform.gameObject.SetActive(true);
            UIServer.Instance.audioManager.playEffect("AudioClips/NormalMordel/Perfect");
            int waves = dataComponent.curWaves;
            this.txtResultShow.text = LanguageUtil.Instance.getFormatString(1002, (waves / 10).ToString(), (waves % 10).ToString(), dataComponent.totalWaves.ToString());
            this.txtLevelShow.text = LanguageUtil.Instance.getFormatString(1003, dataComponent.bigLevel.ToString(), dataComponent.level.ToString());
            this.img_Carrot.sprite = this.carrotSprites[dataComponent.carrotTropyLevel() - 1];
        }

        public void btnEvenReplay()
        {
            GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        public void btnEvenChooseOtherLevel()
        {
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }

        public void Dispose()
        {
            this.RemoveListener();
            this.dataComponent = null;
        }
    }
}


