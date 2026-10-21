using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace CarrotFantasy
{
    /// <summary>
    /// XLua 代码生成配置。
    /// <para>
    /// xLua 生成器（<c>XLua/Generate Code</c>）会扫描所有「静态类 + static 字段」，
    /// 读取字段上的 <see cref="LuaCallCSharpAttribute"/> / <see cref="CSharpCallLuaAttribute"/> /
    /// <see cref="BlackListAttribute"/>，据此决定生成哪些包装、屏蔽哪些成员。
    /// 生成物输出到 <c>Assets/XLua/Gen/</c>（属 XLua.Runtime 程序集）。
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
            typeof(LuaViewBridge),          // Lua 面板唯一直接调用的 C# 对象（在 XLua/Extensions 下）

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

        /// <summary>
        /// 成员级过滤：返回 true 的成员不生成包装。
        /// <para>
        /// <b>为什么不用 <c>List&lt;List&lt;string&gt;&gt;</c> 形式</b>：那种形式匹配方法时，
        /// 除了 {类型, 方法名} 还要求把每个参数类型都逐个列出
        /// （见 <c>Generator.isMethodInBlackList</c>：参数个数须等于 Count-2 且逐个比对），
        /// 写起来啰嗦且容易因类型字符串变化而失效。
        /// </para>
        /// <para>
        /// 当前过滤两类成员：
        /// </para>
        /// <list type="bullet">
        ///   <item>参数或返回值含 <c>Span&lt;T&gt;</c> / <c>ReadOnlySpan&lt;T&gt;</c> 的方法 ——
        ///         Unity 2022+ 的 <c>Transform.TransformDirections/Points/Vectors</c> 等新增了
        ///         Span 重载，而 Span 是 ref struct，不能作泛型实参，生成必然 CS0306；</item>
        ///   <item>指针类型成员 —— xLua 本身也不支持。</item>
        /// </list>
        /// </summary>
        [BlackList]
        public static Func<MemberInfo, bool> UnsupportedMemberFilter = member =>
        {
            MethodBase method = member as MethodBase;
            if (method != null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (IsUnsupportedType(parameters[i].ParameterType))
                    {
                        return true;
                    }
                }

                MethodInfo methodInfo = method as MethodInfo;
                return methodInfo != null && IsUnsupportedType(methodInfo.ReturnType);
            }

            PropertyInfo property = member as PropertyInfo;
            if (property != null)
            {
                return IsUnsupportedType(property.PropertyType);
            }

            FieldInfo field = member as FieldInfo;
            return field != null && IsUnsupportedType(field.FieldType);
        };

        /// <summary>
        /// 是否是 xLua 无法生成的类型：Span / ReadOnlySpan（ref struct 不能作泛型实参）、指针。
        /// <para>用类型全名判断，避免额外引用 System.Memory 等程序集。</para>
        /// </summary>
        private static bool IsUnsupportedType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsPointer)
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            string fullName = type.GetGenericTypeDefinition().FullName;
            return fullName != null
                && (fullName.StartsWith("System.Span`", StringComparison.Ordinal)
                    || fullName.StartsWith("System.ReadOnlySpan`", StringComparison.Ordinal));
        }
    }
}
