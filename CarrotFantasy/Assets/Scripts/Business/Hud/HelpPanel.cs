using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class HelpPanel : BaseView
    {
        private static HelpPanel _instance;
        public static HelpPanel Instance => _instance ?? (_instance = new HelpPanel());

        private HelpPanel() { }

        private GameObject nodeHelp;
        private GameObject nodeMonster;
        private GameObject nodeTower;

        private Button btnReturn;
        private Button btnHelp;
        private Button btnMonster;
        private Button btnTower;

        private Vector3 fadePosition = new Vector3(0, 3000, 0);
        private Vector3 showPosition = Vector3.zero;

        private int showId = 1;

        public override void InitData()
        {
            viewName = "HelpPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.HelpPrefab, "HelpPanel");
        }

        protected override void LoadCallBack()
        {
            this.nodeHelp = this.transform.Find("HelpPage").gameObject;
            this.nodeMonster = this.transform.Find("MonsterPage").gameObject;
            this.nodeTower = this.transform.Find("TowerPage").gameObject;

            this.btnReturn = this.transform.Find("node_top/Btn_Return").GetComponent<Button>();
            this.btnHelp = this.transform.Find("node_top/Btn_Help").GetComponent<Button>();
            this.btnMonster = this.transform.Find("node_top/Btn_Monster").GetComponent<Button>();
            this.btnTower = this.transform.Find("node_top/Btn_Tower").GetComponent<Button>();

            this.showId = 1;
            this.AddListener();
            this.updateNodePosition();
        }

        private void AddListener()
        {
            this.btnReturn.onClick.AddListener(this.Close);
            this.btnHelp.onClick.AddListener(this.showHelpPage);
            this.btnMonster.onClick.AddListener(this.showMonsterPage);
            this.btnTower.onClick.AddListener(this.showTowerPage);
        }

        private void updateNodePosition()
        {
            this.nodeHelp.transform.localPosition = this.showId == 1 ? this.showPosition : this.fadePosition;
            this.nodeMonster.transform.localPosition = this.showId == 2 ? this.showPosition : this.fadePosition;
            this.nodeTower.transform.localPosition = this.showId == 3 ? this.showPosition : this.fadePosition;
        }

        private void showHelpPage()
        {
            this.showId = 1;
            this.updateNodePosition();
        }

        private void showMonsterPage()
        {
            this.showId = 2;
            this.updateNodePosition();
        }

        private void showTowerPage()
        {
            this.showId = 3;
            this.updateNodePosition();
        }

        protected override void ReleaseCallBack()
        {
        }
    }
}
