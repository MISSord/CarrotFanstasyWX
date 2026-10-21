using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// Lua 视图宿主（由 <c>LuaView</c> 实现）。
    /// <para>
    /// 定义在 xLua 运行时程序集里，这样 <see cref="LuaViewBridge"/> 不需要反向引用
    /// 游戏程序集（会形成循环引用），生成的包装代码才能和运行时同程序集编译。
    /// </para>
    /// </summary>
    public interface ILuaViewHost
    {
        void Open(int index);
        void Close();
    }

    /// <summary>
    /// Lua 视图与 C# 的桥。Lua 侧只依赖这四个方法（契约见 <c>framework/view_base.lua</c>）：
    /// <c>bridge:AddButtonClick(go, fn)</c> / <c>bridge:RemoveButtonClick(handle)</c> /
    /// <c>bridge:Open(index)</c> / <c>bridge:Close()</c>。
    /// <para>
    /// 按钮回调统一用 <see cref="LuaFunction"/> 接收，**不需要**生成 CSharpCallLua 代理。
    /// Lua 调本类方法：编辑器下 xLua 走反射即可；IL2CPP 包需要跑一次
    /// <c>XLua/Generate Code</c>（本类已标 <see cref="LuaCallCSharpAttribute"/>）。
    /// </para>
    /// </summary>
    [LuaCallCSharp]
    public sealed class LuaViewBridge
    {
        private struct Binding
        {
            public Button Button;
            public UnityAction Handler;
            public LuaFunction Callback;
        }

        private readonly ILuaViewHost owner;
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
        private int nextHandle = 1;

        public LuaViewBridge(ILuaViewHost owner)
        {
            this.owner = owner;
        }

        /// <summary>给节点上的 Button 绑一个 Lua 回调，返回句柄（供 RemoveButtonClick 使用）。</summary>
        public int AddButtonClick(GameObject node, LuaFunction callback)
        {
            if (node == null || callback == null)
            {
                return 0;
            }

            Button button = node.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[LuaViewBridge] 节点上没有 Button 组件: " + node.name);
                callback.Dispose();
                return 0;
            }

            int handle = this.nextHandle++;
            UnityAction handler = () =>
            {
                try
                {
                    callback.Call();
                }
                catch (Exception e)
                {
                    Debug.LogError("[LuaViewBridge] 按钮回调异常: " + e);
                }
            };

            button.onClick.AddListener(handler);
            this.bindings[handle] = new Binding
            {
                Button = button,
                Handler = handler,
                Callback = callback,
            };
            return handle;
        }

        /// <summary>移除一个按钮回调并释放对应的 LuaFunction。</summary>
        public void RemoveButtonClick(int handle)
        {
            if (!this.bindings.TryGetValue(handle, out Binding binding))
            {
                return;
            }

            if (binding.Button != null)
            {
                binding.Button.onClick.RemoveListener(binding.Handler);
            }

            if (binding.Callback != null)
            {
                binding.Callback.Dispose();
            }

            this.bindings.Remove(handle);
        }

        /// <summary>Lua 侧请求打开（视图已由 C# 创建，这里只是走一次 Open 流程）。</summary>
        public void Open(int index)
        {
            if (this.owner != null)
            {
                this.owner.Open(index);
            }
        }

        /// <summary>Lua 侧请求关闭。</summary>
        public void Close()
        {
            if (this.owner != null)
            {
                this.owner.Close();
            }
        }

        /// <summary>视图释放时的兜底清理（Lua 侧 OnUnload 没清干净的监听在这里摘掉）。</summary>
        public void Release()
        {
            foreach (Binding binding in this.bindings.Values)
            {
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveListener(binding.Handler);
                }

                if (binding.Callback != null)
                {
                    binding.Callback.Dispose();
                }
            }

            this.bindings.Clear();
        }
    }
}
