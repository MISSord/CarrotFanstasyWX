using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// IMGUI（<c>GUIStyle</c> / <c>GUI.Label</c>）用的中文字体解析器。
    /// <para>
    /// Unity 内置的 <c>GUI.skin.font</c> 不含 CJK 字形，所以 GM 控制台、日志控制台这类
    /// 纯 IMGUI 的界面打出来的中文会全部变成方框（口口），而在编辑器里因为能用系统字体回退看不出来。
    /// 这里统一解析一个带中文字形的字体，给相关 <see cref="GUIStyle"/> 显式赋值。
    /// </para>
    /// </summary>
    public static class ImGuiCjkFont
    {
        /// <summary>
        /// 可选：把带中文字形的字体放到 <c>Assets/Resources/Fonts/</c> 下，这里会优先使用它。
        /// 好处是跟着包体走、不依赖目标机装了哪些字体（移动端尤其需要）。
        /// </summary>
        private const string ResourceFontPath = "Fonts/AlibabaPuHuiTi-3-85-Bold";

        /// <summary>系统字体候选，按优先级尝试；桌面平台通常命中第一个。</summary>
        private static readonly string[] OsFontCandidates =
        {
            "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体",
            "Noto Sans CJK SC", "Source Han Sans SC", "PingFang SC", "WenQuanYi Micro Hei",
        };

        private static Font s_font;
        private static bool s_resolved;

        /// <summary>取得中文字体；解析不到时返回 null（不会抛异常）。</summary>
        public static Font Get()
        {
            if (s_resolved)
            {
                return s_font;
            }

            s_resolved = true;

            // 1) 工程内自带字体（前提是已复制到 Resources/Fonts 下）
            try
            {
                s_font = Resources.Load<Font>(ResourceFontPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ImGuiCjkFont] 从 Resources 读取字体失败: " + e.Message);
            }

            // 2) 退回系统字体。桌面平台可用；Android / iOS 没有这些字体名，会返回 null。
            if (s_font == null)
            {
                try
                {
                    s_font = Font.CreateDynamicFontFromOSFont(OsFontCandidates, 16);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[ImGuiCjkFont] 创建系统字体失败: " + e.Message);
                }
            }

            if (s_font == null)
            {
                Debug.LogWarning(
                    "[ImGuiCjkFont] 没有解析到可用的中文字体，IMGUI 界面的中文可能仍显示为方框。"
                    + "建议把带中文字形的 ttf 放到 Assets/Resources/Fonts/ 下。");
            }
            else
            {
                Debug.Log("[ImGuiCjkFont] IMGUI 中文字体已启用: " + s_font.name);
            }

            return s_font;
        }

        /// <summary>
        /// 把中文字体挂到当前 <see cref="GUI.skin"/> 及其常用样式上。
        /// <para>
        /// 除了设 <c>skin.font</c>（供 <c>style.font == null</c> 时回退），还直接把 label / button /
        /// textField / textArea / box / window 这些内置样式的字体也点上，
        /// 避免依赖"空字体自动回退 skin 字体"这一行为在不同 IMGUI 路径上表现不一致。
        /// 对只用 IMGUI 做调试/工具面板的工程来说，全局改内置皮肤是可接受的。
        /// </para>
        /// 建议在每个运行时 IMGUI 面板的 <c>OnGUI</c> 开头调用。
        /// </summary>
        public static void ApplyToCurrentSkin()
        {
            Font font = Get();
            if (font == null)
            {
                return;
            }

            GUISkin skin = GUI.skin;
            if (skin == null)
            {
                return;
            }

            if (skin.font != font)
            {
                skin.font = font;
            }

            ApplyStyleFont(skin.label, font);
            ApplyStyleFont(skin.button, font);
            ApplyStyleFont(skin.textField, font);
            ApplyStyleFont(skin.textArea, font);
            ApplyStyleFont(skin.box, font);
            ApplyStyleFont(skin.window, font);
        }

        private static void ApplyStyleFont(GUIStyle style, Font font)
        {
            if (style != null && style.font != font)
            {
                style.font = font;
            }
        }

        /// <summary>给若干样式套上中文字体；空值安全跳过。</summary>
        public static void Apply(params GUIStyle[] styles)
        {
            Font font = Get();
            if (font == null || styles == null)
            {
                return;
            }

            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] != null)
                {
                    styles[i].font = font;
                }
            }
        }
    }
}
