using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class LoginPanel : BaseView
    {
        private static LoginPanel _instance;
        public static LoginPanel Instance => _instance ?? (_instance = new LoginPanel());

        private LoginPanel() { }

        private Button btnLogin;
        private Button btnResgister;
        private Button btnBack;
        private bool isResigterState = false;
        private InputField inputAccount;
        private InputField inputPassword;
        private InputField inputSurePassword;
        private GameObject nodeInputSurePassword;
        private GameObject nodeBtnBack;
        private GameObject nodeBtnLogin;

        public override void InitData()
        {
            viewName = "LoginPanel";
            layer = UILayer.Normal;
            SetUILoadInfo(0, UiViewAbPaths.LoginPrefab, "LoginPanel");
        }

        protected override void LoadCallBack()
        {
            this.inputAccount = this.transform.Find("node_up/input_account").GetComponent<InputField>();
            this.inputPassword = this.transform.Find("node_up/input_password").GetComponent<InputField>();
            this.nodeInputSurePassword = this.transform.Find("node_up/input_sure_password").gameObject;
            this.inputSurePassword = this.nodeInputSurePassword.transform.GetComponent<InputField>();
            this.nodeBtnBack = this.transform.Find("node_bottom/btn_back").gameObject;
            this.btnBack = this.nodeBtnBack.transform.GetComponent<Button>();
            this.nodeBtnLogin = this.transform.Find("node_bottom/btn_login").gameObject;
            this.btnLogin = this.nodeBtnLogin.transform.GetComponent<Button>();
            this.btnResgister = this.transform.Find("node_bottom/btn_register").GetComponent<Button>();

            this.btnLogin.onClick.AddListener(this.LoginAccount);
            this.btnBack.onClick.AddListener(this.BackLoginState);
            this.btnResgister.onClick.AddListener(this.RegisterEvent);
            this.BackLoginState();
            this.AddListener();
        }

        private void AddListener()
        {
            AccountServer.Instance.eventDispatcher.AddListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
        }

        private void RemoveListener()
        {
            AccountServer.Instance.eventDispatcher.RemoveListener(AccountServer.LOGIN_SUCCESS, this.OnLoginSuccess);
        }

        private void OnLoginSuccess()
        {
            this.Close();
        }

        private void LoginAccount()
        {
            String accountText = this.inputAccount.text;
            String passwordText = this.inputPassword.text;
            if (accountText == null || accountText.Equals("") || passwordText == null || passwordText.Equals(""))
            {
                UIServer.Instance.ShowTip("账号或密码不能为空");
                return;
            }
            AccountServer.Instance.LoginAccount(accountText, passwordText);
        }

        private void RegisterEvent()
        {
            if (this.isResigterState == true)
            {
                this.RegisterAccount();
            }
            else if (this.isResigterState == false)
            {
                this.EnterRegisterAccountState();
            }
        }

        private void RegisterAccount()
        {
            String accountText = this.inputAccount.text;
            String passwordText = this.inputPassword.text;
            String suerpasswordText = this.inputSurePassword.text;
            if (accountText == null || accountText.Equals("") || passwordText == null || passwordText.Equals("") || suerpasswordText == null || suerpasswordText.Equals(""))
            {
                UIServer.Instance.ShowTip("账号或密码不能为空");
                return;
            }
            if (!passwordText.Equals(suerpasswordText))
            {
                UIServer.Instance.ShowTip("两次输入的密码不一样");
                return;
            }
            AccountServer.Instance.RegisterAccount(accountText, passwordText);
        }

        private void EnterRegisterAccountState()
        {
            this.nodeBtnBack.SetActive(true);
            this.nodeBtnLogin.SetActive(false);
            this.nodeInputSurePassword.SetActive(true);
            this.ClearText();
            this.isResigterState = true;
        }

        private void BackLoginState()
        {
            this.nodeBtnBack.SetActive(false);
            this.nodeBtnLogin.SetActive(true);
            this.nodeInputSurePassword.SetActive(false);
            this.ClearText();
            this.isResigterState = false;
        }

        private void ClearText()
        {
            this.inputAccount.text = "";
            this.inputPassword.text = "";
            this.inputSurePassword.text = "";
        }

        protected override void ReleaseCallBack()
        {
            this.RemoveListener();
        }
    }
}
