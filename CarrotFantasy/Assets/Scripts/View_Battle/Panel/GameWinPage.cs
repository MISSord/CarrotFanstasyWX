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
            this.dataComponent = (BattleDataComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.DataComponent);
        }

        public void Init()
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
            this.btnReplay.onClick.AddListener(this.BtnEvenReplay);
            this.btnChooseLevel.onClick.AddListener(this.BtnEvenChooseOtherLevel);
            this.dataComponent.eventDispatcher.AddListener(BattleEvent.SHOW_GAME_FINISH_PAGE, this.ShowGameWinPage);
        }

        private void RemoveListener()
        {
            this.btnReplay.onClick.RemoveAllListeners();
            this.btnChooseLevel.onClick.RemoveAllListeners();
            this.dataComponent.eventDispatcher.RemoveListener(BattleEvent.SHOW_GAME_FINISH_PAGE, this.ShowGameWinPage);
        }

        public void ShowGameWinPage()
        {
            this.transform.gameObject.SetActive(true);
            UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Perfect");
            int waves = dataComponent.curWaves;
            this.txtResultShow.text = LanguageUtil.Instance.GetFormatString(1002, (waves / 10).ToString(), (waves % 10).ToString(), dataComponent.totalWaves.ToString());
            this.txtLevelShow.text = LanguageUtil.Instance.GetFormatString(1003, dataComponent.bigLevel.ToString(), dataComponent.level.ToString());
            this.img_Carrot.sprite = this.carrotSprites[dataComponent.CarrotTropyLevel() - 1];
        }

        public void BtnEvenReplay()
        {
            BattleManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        public void BtnEvenChooseOtherLevel()
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


