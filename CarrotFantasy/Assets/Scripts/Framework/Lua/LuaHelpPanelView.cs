namespace CarrotFantasy
{
    /// <summary>
    /// HelpPanel 的 Lua 版：逻辑全在 <c>panels/help_panel.lua</c>，C# 只做 prefab 加载与桥接。
    /// <para>
    /// 用途是验证「Lua 面板 + 热更」闭环：改 Lua 文件 → 打 lua bundle → 客户端更新后生效，
    /// 不需要重编 C#。开关见 <c>SettingServer.UseLuaHelpPanel</c>。
    /// </para>
    /// </summary>
    public sealed class LuaHelpPanelView : LuaView
    {
        protected override string LuaModule
        {
            get { return "panels.help_panel"; }
        }

        protected override string LuaBundle
        {
            get { return UiViewAbPaths.HelpPrefab; }
        }

        protected override string LuaAsset
        {
            get { return "HelpPanel"; }
        }

        /// <summary>沿用 C# HelpPanel 的名字，既有 <c>OpenView&lt;HelpPanel&gt;()</c> 才能命中。</summary>
        protected override string LuaViewName
        {
            get { return "HelpPanel"; }
        }
    }
}
