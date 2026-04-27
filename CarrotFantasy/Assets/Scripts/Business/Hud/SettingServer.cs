
namespace CarrotFantasy
{
    //设定功能
    public class SettingServer : BaseServer<SettingServer>
    {
        private SetPanel settingPanel;
        private HelpPanel helpPanel;

        public override void LoadModule()
        {
            base.LoadModule();
            settingPanel = new SetPanel();
            settingPanel.RegisterData();

            helpPanel = new HelpPanel();
            helpPanel.RegisterData();
        }

    }
}

