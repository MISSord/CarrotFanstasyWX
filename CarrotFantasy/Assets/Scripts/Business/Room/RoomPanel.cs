using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class RoomPanel : BaseView
    {
        private static RoomPanel _instance;
        public static RoomPanel Instance => _instance ?? (_instance = new RoomPanel());

        private RoomPanel() { }

        private Button btn_fight;
        private Button btn_canel;
        private Text txt_tips;
        private Text txt_userName;
        private Text txt_userState;
        private Text txt_myState;
        private int matchTime;
        private int scheId;

        public override void InitData()
        {
            viewName = "RoomPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.RoomPrefab, "RoomPanel");
        }

        protected override void LoadCallBack()
        {
            this.matchTime = 0;
            this.btn_fight = this.nameTableDic["btn_ready"].GetComponent<Button>();
            this.btn_canel = this.nameTableDic["btn_back"].GetComponent<Button>();

            this.txt_tips = this.nameTableDic["txt_tips"].GetComponent<Text>();
            this.txt_tips.text = LanguageUtil.Instance.GetString(103);

            this.InitUserInfoUI();
            this.AddListener();
            this.scheId = Sche.DelayExeMultipleTimes(this.UpdateTipTxt, 1.0f);
        }

        private void InitUserInfoUI()
        {
            this.txt_userName = this.nameTableDic["txt_user_name_info1"].GetComponent<Text>();
            this.txt_userState = this.nameTableDic["txt_user_name_info2"].GetComponent<Text>();
            this.txt_myState = this.nameTableDic["txt_user_name_info3"].GetComponent<Text>();

            this.txt_userName.text = LanguageUtil.Instance.GetString(102);
            this.txt_userState.text = LanguageUtil.Instance.GetString(100);
            this.txt_myState.text = LanguageUtil.Instance.GetString(100);
        }

        private void AddListener()
        {
            this.btn_canel.onClick.AddListener(this.CanelFight);
            this.btn_fight.onClick.AddListener(this.StateToFight);
            RoomServer.Instance.eventDispatcher.AddListener(RoomEventType.USER_INFO_CHANGE, this.ChangeUserInfo);
        }

        private void RemoveListener()
        {
            RoomServer.Instance.eventDispatcher.RemoveListener(RoomEventType.USER_INFO_CHANGE, this.ChangeUserInfo);
        }

        private void UpdateTipTxt()
        {
            this.matchTime += 1;
            this.txt_tips.text = LanguageUtil.Instance.GetFormatString(104, this.matchTime.ToString());
            if (this.matchTime >= 31)
            {
                this.CanelFight();
            }
        }

        private void ChangeUserInfo()
        {
            if (RoomServer.Instance.partner != null)
            {
                this.txt_userName.text = RoomServer.Instance.partner.UserID.ToString();
                if (RoomServer.Instance.partner.isReady == true)
                {
                    this.txt_userState.text = LanguageUtil.Instance.GetString(101);
                }
                else
                {
                    this.txt_userState.text = LanguageUtil.Instance.GetString(100);
                }
            }
            else
            {
                this.txt_userName.text = LanguageUtil.Instance.GetString(102);
                this.txt_userState.text = LanguageUtil.Instance.GetString(100);
            }
            this.txt_myState.text = LanguageUtil.Instance.GetString(RoomServer.Instance.myself.isReady ? 101 : 100);
        }

        private void StateToFight()
        {
            UIServer.Instance.PlayButtonEffect();
            RoomServer.Instance.SendReadyFight();
        }

        private void CanelFight()
        {
            UIServer.Instance.PlayButtonEffect();
            RoomServer.Instance.CanelMatch();
            this.Close();
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
            Sche.SilenceSingleSche(this.scheId);
        }
    }
}
