using System.Collections.Generic;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class RoomPanel : BasePanel
    {

        private Button btn_fight;
        private Button btn_canel;

        private Text txt_tips;
        private Text txt_userName;
        private Text txt_userState;
        private Text txt_myState;

        private int matchTime = 0;
        private int scheId;


        public RoomPanel(Dictionary<string, dynamic> param) : base(param)
        {
            this.isClickGrayEnable = false;
            this.prefabUrl = "Prefabs/Business/Room/RoomPanel";
        }

        public override void Init()
        {
            base.Init();
            this.panelManagerUnit.registerOnAssetReady(this.OnAssetReady);
            this.panelManagerUnit.registerOnDestroy(this.OnDestroy);
        }

        protected override void OnAssetReady()
        {
            base.OnAssetReady();
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
            //发送准备消息
            UIServer.Instance.playButtonEffect();
            RoomServer.Instance.sendReadyFight();
            //
        }

        private void canelFight()
        {
            //发送取消消息
            UIServer.Instance.playButtonEffect();
            RoomServer.Instance.canelMatch();
            this.Finish();
        }

        protected override void OnDestroy()
        {
            this.RemoveListener();
            Sche.silenceSingleSche(this.scheId);
            base.OnDestroy();
        }
    }
}
