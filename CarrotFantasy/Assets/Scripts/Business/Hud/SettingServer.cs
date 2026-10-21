
namespace CarrotFantasy
{
    //设定功能
    public class SettingServer : BaseServer<SettingServer>
    {
        // 试点开关：true 时帮助面板走 Lua 版（逻辑在 panels/help_panel.lua），
        // 用于验证「Lua 面板 + 热更」闭环；默认 false，行为与原来完全一致。
        private const bool UseLuaHelpPanel = true;

        // 设置面板走 Lua 版（交互流程在 panels/set_panel.lua，
        // 分辨率/音频等设置 API 仍在 C# 的 LuaSetPanelView 里）。
        private const bool UseLuaSetPanel = true;

        private BaseView settingPanel;
        private BaseView helpPanel;
        private StartLoadPanel startLoadPanel;

        public override void LoadModule()
        {
            base.LoadModule();
            settingPanel = UseLuaSetPanel ? (BaseView)new LuaSetPanelView() : new SetPanel();
            settingPanel.RegisterData();

            helpPanel = UseLuaHelpPanel ? (BaseView)new LuaHelpPanelView() : new HelpPanel();
            helpPanel.RegisterData();

            startLoadPanel = new StartLoadPanel();
            startLoadPanel.RegisterData();
        }

    }
}

