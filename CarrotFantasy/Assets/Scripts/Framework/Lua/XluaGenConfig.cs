using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// XLua 代码生成配置。
    /// <para>
    /// xLua 生成器（<c>XLua/Generate Code</c>）会扫描所有「静态类 + static 字段」，
    /// 读取字段上的 <see cref="LuaCallCSharpAttribute"/> / <see cref="CSharpCallLuaAttribute"/>，
    /// 把字段里的 <see cref="Type"/> 列表编译成 Lua 可直接调用的包装（生成到 <c>Assets/XLua/Gen/</c>）。
    /// </para>
    /// <para>
    /// 为什么需要它：没有生成绑定时，xLua 只在编辑器/Mono 下用反射
    /// （ObjectTranslator.cs 的条件编译），IL2CPP 包里 Lua 调 C# 会抛 "not gen"。
    /// </para>
    /// <para>
    /// 维护约定：Lua 侧每新调一个 C# 类型，就往对应列表补一行；改完重新跑一次生成。
    /// </para>
    /// </summary>
    public static class XluaGenConfig
    {
        /// <summary>Lua 调 C# 用到的类型。</summary>
        [LuaCallCSharp]
        public static List<Type> LuaCallCSharp = new List<Type>
        {
            // ---- 本项目 ----
            typeof(LuaViewBridge),          // Lua 面板唯一直接调用的 C# 对象

            // ---- UnityEngine 基础 ----
            typeof(UnityEngine.Object),     // Lua 里判空 / Destroy
            typeof(GameObject),
            typeof(Component),
            typeof(Transform),
            typeof(RectTransform),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Color),
            typeof(Debug),                  // framework/logger.lua 会走 CS.UnityEngine.Debug
            typeof(Time),

            // ---- UGUI（Lua 面板会直接操作控件）----
            typeof(Graphic),
            typeof(Image),
            typeof(RawImage),
            typeof(Text),
            typeof(Button),
            typeof(Slider),
            typeof(Toggle),
            typeof(CanvasGroup),
        };

        /// <summary>
        /// Lua 函数转 C# 委托用到的类型（<c>[CSharpCallLua]</c>）。
        /// <para>
        /// 当前不需要：按钮回调统一用 <see cref="LuaFunction"/> 接收，不生成委托代理。
        /// 以后若要给 C# API 直接传 <c>Action</c>/<c>Func</c>，把对应的委托类型加进来。
        /// </para>
        /// </summary>
        [CSharpCallLua]
        public static List<Type> CSharpCallLua = new List<Type>
        {
        };
    }
}
