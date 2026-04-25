using System;

namespace CarrotFantasy
{
    public class AccountServer : BaseServer
    {
        private static AccountServer accountServer;
        private String account;
        private long gateLoginKey;
        public long userId { get; private set; }
        private bool isInit = false;

        public static String LOGIN_SUCCESS = "Login_success";
        public EventDispatcher eventDispatcher;

        public static AccountServer Instance
        {
            get
            {
                if (accountServer == null)
                {
                    accountServer = new AccountServer();
                    accountServer.eventDispatcher = new EventDispatcher();
                }
                return accountServer;
            }
        }

        public override void LoadModule()
        {
            base.LoadModule();
            this.AddListener();
            this.userId = 0;
        }

        private void AddListener()
        {

        }

        public override void AddSocketListener()
        {
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A0003_LoginGate_G2C, this.notifyLoginGate);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A0002_Login_R2C, this.notifyLogin);
            //ServerProvision.connectionServer.AddListener(HotfixOpcode.A0001_Register_R2C, this.notifyRegister);
        }

        public override void RemoveSocketListener()
        {
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A0003_LoginGate_G2C, this.notifyLoginGate);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A0002_Login_R2C, this.notifyLogin);
            //ServerProvision.connectionServer.RemoveListener(HotfixOpcode.A0001_Register_R2C, this.notifyRegister);
        }

        public void setAccountId(String id)
        {
            if (isInit == false)
            {
                account = id;
            }
            isInit = true;
        }

        public string getAccountId()
        {
            return this.account;
        }

        public override void Dispose()
        {

        }

        public void loginAccount(String accout, String password)
        {
            //ServerProvision.connectionServer.Send(new A0002_Login_C2R() { Account = accout, Password = password });
            this.account = accout;
        }

        public void loginGateAccount()
        {
            //ServerProvision.connectionServer.Send(new A0003_LoginGate_C2G() { GateLoginKey = this.gateLoginKey });
        }

        public void registerAccount(String accout, String password)
        {
            //ServerProvision.connectionServer.Send(new A0001_Register_C2R { Account = accout, Password = password });
        }

        //private void notifyLogin(IMessage message)
        //{
        //    A0002_Login_R2C messageRealm = (A0002_Login_R2C)message;
        //    //判断Realm服务器返回结果
        //    if (messageRealm.Error == ErrorCode.ERR_AccountOrPasswordError)
        //    {
        //        UIServer.Instance.ShowTip("登录失败,账号或密码错误");
        //        return;
        //    }
        //    this.setAccountId(this.account);
        //    this.gateLoginKey = messageRealm.GateLoginKey;
        //    this.loginGateAccount();
        //}

        //private void notifyRegister(IMessage message)
        //{
        //    A0001_Register_R2C messageRealm = (A0001_Register_R2C)message;
        //    if (messageRealm.Error == ErrorCode.ERR_AccountAlreadyRegisted)
        //    {
        //        UIServer.Instance.ShowTip("注册失败，账号已被注册");
        //        return;
        //    }

        //    if (messageRealm.Error == ErrorCode.ERR_RepeatedAccountExist)
        //    {
        //        UIServer.Instance.ShowTip("注册失败，出现重复账号");
        //        return;
        //    }

        //    //显示登录成功的提示
        //    UIServer.Instance.ShowTip("注册成功");
        //}

        //private void notifyLoginGate(IMessage message)
        //{
        //    A0003_LoginGate_G2C msg = (A0003_LoginGate_G2C)message;
        //    this.userId = msg.UserID;
        //    this.eventDispatcher.DispatchEvent(LOGIN_SUCCESS);
        //    UIServer.Instance.ShowTip("登录成功,祝你游玩愉快");
        //}
    }
}
