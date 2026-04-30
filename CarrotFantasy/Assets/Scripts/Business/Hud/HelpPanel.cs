using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class HelpPanel : BaseView
    {
        private GameObject nodeHelp;
        private GameObject nodeMonster;
        private GameObject nodeTower;

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
            this.nodeHelp = this.nameTableDic["HelpPage"];
            this.nodeMonster = this.nameTableDic["MonsterPage"];
            this.nodeTower = this.nameTableDic["TowerPage"];

            this.showId = 1;
            this.AddListener();
            this.UpdateNodePosition();
        }

        private void AddListener()
        {
            XUI.AddButtonListener(this.nameTableDic["Btn_Return"].GetComponent<Button>(), this.Close);
            XUI.AddButtonListener(this.nameTableDic["Btn_Help"].GetComponent<Button>(), this.ShowHelpPage);
            XUI.AddButtonListener(this.nameTableDic["Btn_Monster"].GetComponent<Button>(), this.ShowMonsterPage);
            XUI.AddButtonListener(this.nameTableDic["Btn_Tower"].GetComponent<Button>(), this.ShowTowerPage);
        }

        private void UpdateNodePosition()
        {
            this.nodeHelp.transform.localPosition = this.showId == 1 ? this.showPosition : this.fadePosition;
            this.nodeMonster.transform.localPosition = this.showId == 2 ? this.showPosition : this.fadePosition;
            this.nodeTower.transform.localPosition = this.showId == 3 ? this.showPosition : this.fadePosition;
        }

        private void ShowHelpPage()
        {
            this.showId = 1;
            this.UpdateNodePosition();
        }

        private void ShowMonsterPage()
        {
            this.showId = 2;
            this.UpdateNodePosition();
        }

        private void ShowTowerPage()
        {
            this.showId = 3;
            this.UpdateNodePosition();
        }

        protected override void ReleaseCallBack()
        {
        }
    }
}
