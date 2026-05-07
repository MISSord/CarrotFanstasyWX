using System;
using CfNet;
using UnityEngine;

namespace CarrotFantasy
{
    public class AccountServer : BaseServer<AccountServer>
    {
        private String account;
        public long userId { get; private set; }
        private bool isInit = false;

        public static String LOGIN_SUCCESS = "Login_success";
        public EventDispatcher eventDispatcher;

        private MainPanel mainPanel;

        protected override void OnSingletonInit()
        {
            eventDispatcher = new EventDispatcher();
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.AddListener();
            this.userId = 0;

            mainPanel = new MainPanel();
            mainPanel.RegisterData();
        }

        private void AddListener()
        {

        }

        public override void AddSocketListener()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs != null)
            {
                cs.AddProtobufListener(SimpleBinaryOpcodes.LoginResponse, LoginResponse.Parser, this.OnLoginResponseProto);
            }
        }

        public override void RemoveSocketListener()
        {
            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs != null)
            {
                cs.RemoveProtobufListener(SimpleBinaryOpcodes.LoginResponse);
            }
        }

        public void SetAccountId(String id)
        {
            if (isInit == false)
            {
                account = id;
            }

            isInit = true;
        }

        public string GetAccountId()
        {
            return this.account;
        }

        public override void Dispose()
        {
            mainPanel.DeleteMe();
            mainPanel = null;

        }

        public void LoginAccount(String accout, String password)
        {
            this.SetAccountId(accout);

            ConnectionServer cs = ServerProvision.connectionServer;
            if (cs == null)
            {
                Debug.LogWarning("LoginAccount: ConnectionServer 未初始化。");
                return;
            }

            try
            {
                var req = new LoginRequest
                {
                    Account = accout ?? string.Empty,
                    Password = password ?? string.Empty,
                };
                cs.SendProtobuf(SimpleBinaryOpcodes.LoginRequest, req);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("LoginAccount: 发送失败 {0}", ex.Message));
                UIServer.Instance.ShowTip("登录请求无效");
            }
        }

        private void OnLoginResponseProto(LoginResponse response)
        {
            if (response.Result == 0)
            {
                this.userId = response.UserId;
                this.eventDispatcher.DispatchEvent(LOGIN_SUCCESS);
                string tip = string.IsNullOrEmpty(response.Message) ? "登录成功,祝你游玩愉快" : response.Message;
                UIServer.Instance.ShowTip(tip);
                return;
            }

            string fail = string.IsNullOrEmpty(response.Message) ? "登录失败" : response.Message;
            UIServer.Instance.ShowTip(fail);
        }

        public void LoginGateAccount()
        {
            // 网关二阶段鉴权可在此扩展。
        }

        public void RegisterAccount(String accout, String password)
        {
            // 注册：在 GameNetwork.proto 增加 Register 消息后在此 SendProtobuf。
        }
    }
}
