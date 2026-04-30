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
            this.inputAccount = this.nameTableDic["input_account"].GetComponent<InputField>();
            this.inputPassword = this.nameTableDic["input_password"].GetComponent<InputField>();
            this.nodeInputSurePassword = this.nameTableDic["input_sure_password"];
            this.inputSurePassword = this.nodeInputSurePassword.GetComponent<InputField>();
            this.nodeBtnBack = this.nameTableDic["btn_back"];
            this.nodeBtnLogin = this.nameTableDic["btn_login"];

            XUI.AddButtonListener(this.nodeBtnLogin.GetComponent<Button>(), this.LoginAccount);
            XUI.AddButtonListener(this.nodeBtnBack.GetComponent<Button>(), this.BackLoginState);
            XUI.AddButtonListener(this.nameTableDic["btn_register"].GetComponent<Button>(), this.RegisterEvent);
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
