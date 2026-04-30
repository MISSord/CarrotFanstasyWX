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

        public override void InitData()
        {
            viewName = "MainPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MainViewViewPrefab, "MainPanel");
        }

        protected override void LoadCallBack()
        {
            transform.SetSiblingIndex(8);
            this.carrotAnimator = this.nameTableDic["node_carrot"].GetComponent<Animator>();
            this.carrotAnimator.Play("CarrotGrow");
            this.monsterTrans = this.nameTableDic["spr_monster"].transform;
            this.cloudTrans = this.nameTableDic["spr_cloud"].transform;

            mainPanelTween = new Tween[2];
            mainPanelTween[0] = transform.DOLocalMoveX(1920, 0.5f);
            mainPanelTween[0].SetAutoKill(false);
            mainPanelTween[0].Pause();
            mainPanelTween[1] = transform.DOLocalMoveX(-1920, 0.5f);
            mainPanelTween[1].SetAutoKill(false);
            mainPanelTween[1].Pause();

            this.AddListener();
            this.PlayUITween();
            UIServer.Instance.PlayMainBg();
        }

        private void AddListener()
        {
            Button btnNormal = this.nameTableDic["btn_normal"].GetComponent<Button>();
            Button btnBoss = this.nameTableDic["btn_boss"].GetComponent<Button>();
            Button btnNetwork = this.nameTableDic["btn_network"].GetComponent<Button>();

            Button btnExitGame = this.nameTableDic["btn_exit_game"].GetComponent<Button>();
            Button btnHelp = this.nameTableDic["btn_help"].GetComponent<Button>();
            Button btnSet = this.nameTableDic["btn_set"].GetComponent<Button>();

            XUI.AddButtonListener(btnBoss, this.ToBossModel);
            XUI.AddButtonListener(btnNormal, this.ToNormalModel);
            XUI.AddButtonListener(btnNetwork, this.StartMatch);
            XUI.AddButtonListener(btnExitGame, this.ExitGame);
            XUI.AddButtonListener(btnHelp, this.ShowHelpPanel);
            XUI.AddButtonListener(btnSet, this.ShowSetPanel);
        }

        private void PlayUITween()
        {
            this.monsterTrans.DOLocalMoveY(20, 7f).SetLoops(-1, LoopType.Yoyo);
            this.cloudTrans.DOLocalMoveX(1300, 30f).SetLoops(-1, LoopType.Restart);
        }

        public void ShowSetPanel()
        {
            UIServer.Instance.PlayButtonEffect();
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
