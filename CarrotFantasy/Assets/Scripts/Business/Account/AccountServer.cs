using System;
using CfNet;
using UnityEngine;

namespace CarrotFantasy
{
    public class AccountServer : BaseServer<AccountServer>
    {
        private String account;
        private string cachedPassword = string.Empty;
        public long userId { get; private set; }
        private bool isInit = false;

        public static String LOGIN_SUCCESS = "Login_success";
        public static String LOGIN_FAILED = "Login_failed";
        public static String SESSION_EXPIRED = "Session_expired";
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
            this.account = id;
            this.isInit = true;
        }

        public string GetAccountId()
        {
            return this.account;
        }

        public void CacheCredentials(string accout, string password)
        {
            this.account = accout ?? string.Empty;
            this.cachedPassword = password ?? string.Empty;
            this.isInit = true;
        }

        public bool TryReloginWithCachedCredentials()
        {
            if (StandaloneGameConfig.EnableStandaloneMode)
            {
                return false;
            }

            if (string.IsNullOrEmpty(this.account) || string.IsNullOrEmpty(this.cachedPassword))
            {
                return false;
            }

            this.LoginAccount(this.account, this.cachedPassword);
            return true;
        }

        public override void Dispose()
        {
            mainPanel.DeleteMe();
            mainPanel = null;

        }

        public void ApplyStandaloneSession(string account, long userId)
        {
            this.SetAccountId(account);
            this.userId = userId;
        }

        public void LoginAccount(String accout, String password)
        {
            if (StandaloneGameConfig.EnableStandaloneMode)
            {
                StandaloneBackendMock.SimulateLogin(accout, password);
                return;
            }

            this.CacheCredentials(accout, password);

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
                this.eventDispatcher.DispatchEvent(LOGIN_FAILED);
            }
        }

        private void OnLoginResponseProto(LoginResponse response)
        {
            if (response.Result == 0)
            {
                this.userId = response.UserId;
                this.eventDispatcher.DispatchEvent(LOGIN_SUCCESS);

                bool isReconnecting = ServerProvision.connectionLifecycle != null
                    && ServerProvision.connectionLifecycle.IsReconnecting;
                if (!isReconnecting)
                {
                    string tip = string.IsNullOrEmpty(response.Message) ? "登录成功,祝你游玩愉快" : response.Message;
                    UIServer.Instance.ShowTip(tip);
                }

                return;
            }

            string fail = string.IsNullOrEmpty(response.Message) ? "登录失败" : response.Message;
            UIServer.Instance.ShowTip(fail);
            this.eventDispatcher.DispatchEvent(LOGIN_FAILED);
        }

        /// <summary>联机会话失效：清凭证、断连接、回登录界面。</summary>
        public void ForceLogout(string reason)
        {
            ServerProvision.connectionLifecycle?.StopMonitoring();

            ConnectionServer cs = ServerProvision.connectionServer;
            cs?.StopConnection();

            this.userId = 0;
            this.account = string.Empty;
            this.cachedPassword = string.Empty;
            this.isInit = false;

            this.eventDispatcher.DispatchEvent(SESSION_EXPIRED);

            OnlineSessionRecovery.ReturnToOnlineLogin(reason);
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
