using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// 逻辑写在 Lua 里的视图基类：C# 只负责加载 prefab、构建 name → 节点表、驱动生命周期，
    /// 业务逻辑在 <see cref="LuaModule"/> 指向的 Lua 模块里。
    /// <para>
    /// 生命周期映射（Lua 侧方法名见 <c>framework/view_base.lua</c>）：
    /// </para>
    /// <list type="table">
    ///   <item><term>LoadCallBack</term><description>BindBridge + HandleLoaded(root, ui)</description></item>
    ///   <item><term>OpenCallBack(index)</term><description>OnOpenCallback(index)</description></item>
    ///   <item><term>OnFlush(index, info)</term><description>HandleFlushed(index, info)</description></item>
    ///   <item><term>OnPause / OnResume</term><description>ApplyPauseState(false / true)</description></item>
    ///   <item><term>CloseCallBack</term><description>HandleClosed</description></item>
    ///   <item><term>ReleaseCallBack</term><description>HandleReleased + 释放桥与 Lua 引用</description></item>
    /// </list>
    /// </summary>
    public abstract class LuaView : BaseView, ILuaViewHost
    {
        /// <summary>Lua 模块名（require 路径），如 <c>panels.help_panel</c>。</summary>
        protected abstract string LuaModule { get; }

        /// <summary>prefab 所在 AB 包名。</summary>
        protected abstract string LuaBundle { get; }

        /// <summary>prefab 资源名。</summary>
        protected abstract string LuaAsset { get; }

        /// <summary>
        /// 对外的视图名（注册进 ViewManager 的名字）。
        /// <para>
        /// 默认用 C# 类型名；Lua 版沿用被替换的那个 C# 面板名（如 "HelpPanel"），
        /// 这样 <c>OpenView&lt;HelpPanel&gt;()</c> 这类既有调用不用改。
        /// </para>
        /// </summary>
        protected virtual string LuaViewName
        {
            get { return GetType().Name; }
        }

        private LuaTable luaView;
        private LuaViewBridge bridge;

        public override void InitData()
        {
            viewName = LuaViewName;
            SetUILoadInfo(0, LuaBundle, LuaAsset);
        }

        protected override void LoadCallBack()
        {
            this.luaView = LuaManager.EnsureLuaView(LuaModule, this.BuildLuaViewConfig());
            if (this.luaView == null)
            {
                Debug.LogError("[LuaView] 创建 Lua 视图失败: " + LuaModule);
                return;
            }

            this.bridge = new LuaViewBridge(this);
            this.CallLua("BindBridge", this.bridge);

            // host 是 C# 提供给 Lua 的业务能力（委托表），没有就不注入。
            LuaTable host = this.BuildLuaHost();
            if (host != null)
            {
                this.CallLua("BindHost", host);
            }
            this.CallLua(
                "HandleLoaded",
                this.transform != null ? this.transform.gameObject : null,
                this.BuildLuaNodeTable());
        }

        protected override void OpenCallBack(int index)
        {
            this.CallLua("OnOpenCallback", index);
        }

        protected override void OnFlush(int index, Dictionary<string, string> info)
        {
            this.CallLua("HandleFlushed", index, BuildLuaInfoTable(info));
        }

        protected override void OnPause()
        {
            this.CallLua("ApplyPauseState", false);
        }

        protected override void OnResume()
        {
            this.CallLua("ApplyPauseState", true);
        }

        protected override void CloseCallBack()
        {
            this.CallLua("HandleClosed");
        }

        protected override void ReleaseCallBack()
        {
            // 先让 Lua 侧解绑（OnUnload 里可能调 bridge:RemoveButtonClick），再兜底清理。
            this.CallLua("HandleReleased");

            if (this.bridge != null)
            {
                this.bridge.Release();
                this.bridge = null;
            }

            if (this.luaView != null)
            {
                this.luaView.Dispose();
                this.luaView = null;
            }
        }

        /// <summary>
        /// 传给 Lua 的构造配置，由 <c>BaseView.__init(cfg)</c> 消费。
        /// <para>
        /// 这样 Lua 子类**不需要**覆盖 <c>New</c>：配置从 C# 传下来，
        /// 子类只在 <c>__init</c> 里初始化自己的字段（父类 __init 由 class() 自动先调用）。
        /// </para>
        /// </summary>
        private LuaTable BuildLuaViewConfig()
        {
            LuaTable cfg = LuaManager.NewTable();
            if (cfg == null)
            {
                return null;
            }

            cfg.Set("name", this.LuaViewName);
            cfg.Set("layer", (int)this.Layer);
            cfg.Set("bundle", this.LuaBundle);
            cfg.Set("asset", this.LuaAsset);
            return cfg;
        }

        /// <summary>
        /// 构建给 Lua 用的 host 表（值是 C# 委托），子类覆写后可向 Lua 暴露业务能力。
        /// <para>
        /// 委托类型需要登记在 <c>XluaGenConfig.CSharpCallLua</c>，否则 IL2CPP 下无法转成 Lua 函数。
        /// </para>
        /// </summary>
        protected virtual LuaTable BuildLuaHost()
        {
            return null;
        }

        /// <summary>nameTableDic → Lua 表，Lua 侧通过 <c>self:Node('Btn_Return')</c> 取节点。</summary>
        private LuaTable BuildLuaNodeTable()
        {
            LuaTable table = LuaManager.NewTable();
            if (table == null || this.nameTableDic == null)
            {
                return table;
            }

            foreach (KeyValuePair<string, GameObject> entry in this.nameTableDic.Entries)
            {
                table.Set(entry.Key, entry.Value);
            }

            return table;
        }

        private static LuaTable BuildLuaInfoTable(Dictionary<string, string> info)
        {
            LuaTable table = LuaManager.NewTable();
            if (table == null || info == null)
            {
                return table;
            }

            foreach (KeyValuePair<string, string> entry in info)
            {
                table.Set(entry.Key, entry.Value);
            }

            return table;
        }

        private void CallLua(string method, params object[] args)
        {
            if (this.luaView == null)
            {
                return;
            }

            LuaManager.CallLuaView(this.luaView, method, args);
        }
    }
}
