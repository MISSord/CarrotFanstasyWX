using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class MainPanel : BaseView
    {
        private Animator carrotAnimator;
        private Transform monsterTrans;
        private Transform cloudTrans;
        private SimpleTween[] mainPanelTween;

        public override void InitData()
        {
            viewName = "MainPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.MainViewViewPrefab, "MainPanel");
        }

        protected override void LoadCallBack()
        {
            this.carrotAnimator = this.nameTableDic["node_carrot"].GetComponent<Animator>();
            this.carrotAnimator.Play("CarrotGrow");
            this.monsterTrans = this.nameTableDic["spr_monster"].transform;
            this.cloudTrans = this.nameTableDic["spr_cloud"].transform;

            mainPanelTween = new SimpleTween[2];
            mainPanelTween[0] = SimpleTween.LocalMoveX(transform, 1920, 0.5f);
            mainPanelTween[0].SetAutoKill(false);
            mainPanelTween[0].Pause();
            mainPanelTween[1] = SimpleTween.LocalMoveX(transform, -1920, 0.5f);
            mainPanelTween[1].SetAutoKill(false);
            mainPanelTween[1].Pause();

            this.AddListener();
            this.PlayUITween();
            UIServer.Instance.PlayMainBg();
        }

        private void AddListener()
        {
            Button btnNormal = this.nameTableDic["btn_normal"].GetComponent<Button>();
            Button btnRoguelike = this.nameTableDic["btn_roguelike"].GetComponent<Button>();
            Button btnExitGame = this.nameTableDic["btn_exit_game"].GetComponent<Button>();
            Button btnHelp = this.nameTableDic["btn_help"].GetComponent<Button>();
            Button btnSet = this.nameTableDic["btn_set"].GetComponent<Button>();

            XUI.AddButtonListener(btnRoguelike, this.ToRoguelikeModel);
            XUI.AddButtonListener(btnNormal, this.ToNormalModel);
            XUI.AddButtonListener(btnExitGame, this.ExitGame);
            XUI.AddButtonListener(btnHelp, this.ShowHelpPanel);
            XUI.AddButtonListener(btnSet, this.ShowSetPanel);
        }

        private void PlayUITween()
        {
            if (this.monsterTrans != null)
            {
                SimpleTween.Kill(this.monsterTrans, false);
                SimpleTween.LocalMoveY(this.monsterTrans, 20, 7f).SetLoops(-1, SimpleLoopType.Yoyo).Play();
            }

            if (this.cloudTrans != null)
            {
                SimpleTween.Kill(this.cloudTrans, false);
                SimpleTween.LocalMoveX(this.cloudTrans, 1300, 30f).SetLoops(-1, SimpleLoopType.Restart).Play();
            }
        }

        public void ShowSetPanel()
        {
            ViewManager.Instance.OpenView<SetPanel>();
        }

        public void ShowHelpPanel()
        {
            ViewManager.Instance.OpenView<HelpPanel>();
        }

        public void ToNormalModel()
        {
            ViewManager.Instance.OpenView<MapBigLevelPanel>();
        }

        public void ToRoguelikeModel()
        {
            ViewManager.Instance.OpenView<RoguelikeBigLevelPanel>();
        }

        public void ExitGame()
        {
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.GAME_QUIT);
        }

        protected override void ReleaseCallBack()
        {
            if (this.monsterTrans != null)
            {
                SimpleTween.Kill(this.monsterTrans, false);
            }

            if (this.cloudTrans != null)
            {
                SimpleTween.Kill(this.cloudTrans, false);
            }

            if (this.transform != null)
            {
                SimpleTween.Kill(this.transform, false);
            }
        }
    }
}
