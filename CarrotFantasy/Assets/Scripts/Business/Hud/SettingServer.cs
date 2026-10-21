
namespace CarrotFantasy
{
    //设定功能
    public class SettingServer : BaseServer<SettingServer>
    {
        // 试点开关：true 时帮助面板走 Lua 版（逻辑在 panels/help_panel.lua），
        // 用于验证「Lua 面板 + 热更」闭环；默认 false，行为与原来完全一致。
        private const bool UseLuaHelpPanel = false;

        private SetPanel settingPanel;
        private BaseView helpPanel;
        private StartLoadPanel startLoadPanel;

        public override void LoadModule()
        {
            base.LoadModule();
            settingPanel = new SetPanel();
            settingPanel.RegisterData();

            helpPanel = UseLuaHelpPanel ? (BaseView)new LuaHelpPanelView() : new HelpPanel();
            helpPanel.RegisterData();

            startLoadPanel = new StartLoadPanel();
            startLoadPanel.RegisterData();
        }

    }
}

