using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>Buff 表现层样式（图标颜色与缩写，与逻辑 <see cref="BuffCategory"/> 对应）。</summary>
    public static class BuffViewVisualCatalog
    {
        public struct BuffVisualStyle
        {
            public string label;
            public Color iconColor;
        }

        public static bool TryGetStyle(BuffCategory category, out BuffVisualStyle style)
        {
            switch (category)
            {
                case BuffCategory.Slow:
                    style = new BuffVisualStyle { label = "慢", iconColor = new Color(0.35f, 0.65f, 1f, 0.92f) };
                    return true;
                case BuffCategory.Dot:
                    style = new BuffVisualStyle { label = "毒", iconColor = new Color(0.45f, 0.9f, 0.35f, 0.92f) };
                    return true;
                case BuffCategory.Stun:
                    style = new BuffVisualStyle { label = "晕", iconColor = new Color(0.95f, 0.85f, 0.25f, 0.92f) };
                    return true;
                case BuffCategory.DamageAmp:
                    style = new BuffVisualStyle { label = "破", iconColor = new Color(1f, 0.45f, 0.35f, 0.92f) };
                    return true;
                default:
                    style = new BuffVisualStyle { label = "?", iconColor = new Color(0.75f, 0.75f, 0.75f, 0.85f) };
                    return false;
            }
        }

        public static bool TryGetStyle(int buffId, out BuffVisualStyle style)
        {
            if (BuffConfigReader.Instance.TryGetDef(buffId, out BuffDef def))
            {
                return TryGetStyle(def.category, out style);
            }

            style = new BuffVisualStyle { label = buffId.ToString(), iconColor = Color.gray };
            return false;
        }
    }
}
