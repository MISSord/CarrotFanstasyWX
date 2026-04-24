using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace CarrotFantasy
{
    /// <summary>
    /// 菜单页面
    /// </summary>
    public class MenuPage
    {
        private Transform transform;

        private Button btnGoOn;
        private Button btnReplay;
        private Button btnChooseLevel;

        public MenuPage(Transform node)
        {
            this.transform = node;
        }

        public void init()
        {
            this.btnGoOn = this.transform.Find("btn_go_on").GetComponent<Button>();
            this.btnReplay = this.transform.Find("btn_replay").GetComponent<Button>();
            this.btnChooseLevel = this.transform.Find("btn_choose_level").GetComponent<Button>();

            this.btnGoOn.onClick.AddListener(this.btnEvenGoOn);
            this.btnReplay.onClick.AddListener(this.btnEvenReplay);
            this.btnChooseLevel.onClick.AddListener(this.btnEvenChooseOtherLevel);
        }

        public void btnEvenGoOn()
        {
            UIServer.Instance.playButtonEffect();
            this.transform.gameObject.SetActive(false);
            GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.GO_ON_GAME);
        }

        public void btnEvenReplay()
        {
            UIServer.Instance.playButtonEffect();
            this.transform.gameObject.SetActive(false);
            GameManager.Instance.baseBattle.eventDispatcher.DispatchEvent(BattleEvent.REPLAY_THE_GAME);
        }

        public void btnEvenChooseOtherLevel()
        {
            UIServer.Instance.playButtonEffect();
            this.transform.gameObject.SetActive(false);
            BusinessProvision.Instance.eventDispatcher.DispatchEvent(CommonEventType.RETURN_TO_MAIN_SCENE);
        }
    }
}

