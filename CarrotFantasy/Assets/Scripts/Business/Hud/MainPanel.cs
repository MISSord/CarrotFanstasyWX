using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MainPanel : BaseView
    {
        private Animator carrotAnimator;
        private Transform monsterTrans;
        private Transform cloudTrans;
        private Tween[] mainPanelTween;
        private Tween ExitTween;

        private Button btnNormal;
        private Button btnBoss;
        private Button btnNetwork;

        private Button btnExitGame;
        private Button btnSet;
        private Button btnHelp;

        public override void InitData()
        {
            viewName = "MainPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MainViewViewPrefab, "MainPanel");
        }

        protected override void LoadCallBack()
        {
            transform.SetSiblingIndex(8);
            this.carrotAnimator = transform.Find("node_center/node_carrot").GetComponent<Animator>();
            this.carrotAnimator.Play("CarrotGrow");
            this.monsterTrans = transform.Find("node_top/spr_monster");
            this.cloudTrans = transform.Find("node_top/spr_cloud");

            mainPanelTween = new Tween[2];
            mainPanelTween[0] = transform.DOLocalMoveX(1920, 0.5f);
            mainPanelTween[0].SetAutoKill(false);
            mainPanelTween[0].Pause();
            mainPanelTween[1] = transform.DOLocalMoveX(-1920, 0.5f);
            mainPanelTween[1].SetAutoKill(false);
            mainPanelTween[1].Pause();

            this.btnNormal = this.transform.Find("node_bottom/btn_normal").GetComponent<Button>();
            this.btnBoss = this.transform.Find("node_bottom/btn_boss").GetComponent<Button>();
            this.btnNetwork = this.transform.Find("node_bottom/btn_network").GetComponent<Button>();

            this.btnExitGame = this.transform.Find("node_top/btn_exit_game").GetComponent<Button>();
            this.btnHelp = this.transform.Find("node_center/btn_help").GetComponent<Button>();
            this.btnSet = this.transform.Find("node_center/btn_set").GetComponent<Button>();

            this.AddListener();
            this.PlayUITween();
            UIServer.Instance.PlayMainBg();
        }

        private void AddListener()
        {
            this.btnBoss.onClick.AddListener(this.ToBossModel);
            this.btnNormal.onClick.AddListener(this.ToNormalModel);
            this.btnNetwork.onClick.AddListener(this.StartMatch);

            this.btnExitGame.onClick.AddListener(this.ExitGame);
            this.btnHelp.onClick.AddListener(this.ShowHelpPanel);
            this.btnSet.onClick.AddListener(this.ShowSetPanel);
        }

        private void PlayUITween()
        {
            this.monsterTrans.DOLocalMoveY(20, 7f).SetLoops(-1, LoopType.Yoyo);
            this.cloudTrans.DOLocalMoveX(1300, 30f).SetLoops(-1, LoopType.Restart);
        }

        public void ShowSetPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            ExitTween = mainPanelTween[0];
            ViewManager.Instance.OpenView<SetPanel>();
        }

        public void ShowHelpPanel()
        {
            UIServer.Instance.PlayButtonEffect();
            ViewManager.Instance.OpenView<HelpPanel>();
        }

        public void ToNormalModel()
        {
            UIServer.Instance.PlayButtonEffect();
            UIViewService.OpenMapBigLevelPanel();
        }

        public void ToBossModel()
        {
            UIServer.Instance.PlayButtonEffect();
        }

        private void StartMatch()
        {
            UIViewService.OpenRoomPanel();
            RoomServer.Instance.SendStartMatch();
            UIServer.Instance.PlayButtonEffect();
        }

        public void ExitGame()
        {
            UIServer.Instance.PlayButtonEffect();
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.GAME_QUIT);
        }

        protected override void ReleaseCallBack()
        {
        }
    }
}
