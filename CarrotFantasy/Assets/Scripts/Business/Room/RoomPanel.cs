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
            this.btn_fight = this.transform.Find("node_bottom/btn_ready").GetComponent<Button>();
            this.btn_canel = this.transform.Find("node_bottom/btn_back").GetComponent<Button>();

            this.txt_tips = this.transform.Find("node_up/txt_tips").GetComponent<Text>();
            this.txt_tips.text = LanguageUtil.Instance.getString(103);

            this.initUserInfoUI();
            this.AddListener();
            this.scheId = Sche.delayExeMultipleTimes(this.updateTipTxt, 1.0f);
        }

        private void initUserInfoUI()
        {
            this.txt_userName = this.transform.Find("node_center/node_info/node_info1/txt_user_name").GetComponent<Text>();
            this.txt_userState = this.transform.Find("node_center/node_info/node_info2/txt_user_name").GetComponent<Text>();
            this.txt_myState = this.transform.Find("node_center/node_info/node_info3/txt_user_name").GetComponent<Text>();

            this.txt_userName.text = LanguageUtil.Instance.getString(102);
            this.txt_userState.text = LanguageUtil.Instance.getString(100);
            this.txt_myState.text = LanguageUtil.Instance.getString(100);
        }

        private void AddListener()
        {
            this.btn_canel.onClick.AddListener(this.canelFight);
            this.btn_fight.onClick.AddListener(this.stateToFight);
            RoomServer.Instance.eventDispatcher.AddListener(RoomEventType.USER_INFO_CHANGE, this.changeUserInfo);
        }

        private void RemoveListener()
        {
            RoomServer.Instance.eventDispatcher.RemoveListener(RoomEventType.USER_INFO_CHANGE, this.changeUserInfo);
        }

        private void updateTipTxt()
        {
            this.matchTime += 1;
            this.txt_tips.text = LanguageUtil.Instance.getFormatString(104, this.matchTime.ToString());
            if (this.matchTime >= 31)
            {
                this.canelFight();
            }
        }

        private void changeUserInfo()
        {
            if (RoomServer.Instance.partner != null)
            {
                this.txt_userName.text = RoomServer.Instance.partner.UserID.ToString();
                if (RoomServer.Instance.partner.isReady == true)
                {
                    this.txt_userState.text = LanguageUtil.Instance.getString(101);
                }
                else
                {
                    this.txt_userState.text = LanguageUtil.Instance.getString(100);
                }
            }
            else
            {
                this.txt_userName.text = LanguageUtil.Instance.getString(102);
                this.txt_userState.text = LanguageUtil.Instance.getString(100);
            }
            this.txt_myState.text = LanguageUtil.Instance.getString(RoomServer.Instance.myself.isReady ? 101 : 100);
        }

        private void stateToFight()
        {
            UIServer.Instance.PlayButtonEffect();
            RoomServer.Instance.sendReadyFight();
        }

        private void canelFight()
        {
            UIServer.Instance.PlayButtonEffect();
            RoomServer.Instance.canelMatch();
            this.Close();
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
            Sche.silenceSingleSche(this.scheId);
        }
    }
}
