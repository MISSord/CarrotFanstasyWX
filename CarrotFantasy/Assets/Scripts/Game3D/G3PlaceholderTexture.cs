using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 占位纹理生成：为 2D 立绘单位生成一张简易人形剪影图，
    /// 便于在美术资源就绪前验证 3D 场景 + 2D 立绘的渲染框架效果。
    /// </summary>
    public static class G3PlaceholderTexture
    {
        static Texture2D cached;

        public static Texture2D Default
        {
            get
            {
                if (cached == null)
                {
                    cached = Create(128, 128);
                }
                return cached;
            }
        }

        public static Texture2D Create(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "G3 Placeholder Unit";
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color body = new Color(0.85f, 0.85f, 0.9f, 1f);
            Color accent = new Color(0.35f, 0.55f, 0.85f, 1f);

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    pixels[y * width + x] = SampleUnit(u, v, clear, body, accent);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static Color SampleUnit(float u, float v, Color clear, Color body, Color accent)
        {
            // 画一个人形：头（圆）+ 躯干 + 腿 + 裙摆
            float cx = 0.5f;
            float headCenterY = 0.80f;
            float headR = 0.12f;

            // 头
            float dx = u - cx;
            float dy = v - headCenterY;
            if (dx * dx + dy * dy < headR * headR)
            {
                return body;
            }

            // 躯干（梯形）
            if (v < 0.78f && v > 0.42f)
            {
                float halfW = Mathf.Lerp(0.16f, 0.24f, (v - 0.42f) / 0.36f);
                if (Mathf.Abs(u - cx) < halfW)
                {
                    return body;
                }
            }

            // 腿（两窄条）
            if (v <= 0.42f && v > 0.22f)
            {
                float legW = 0.05f;
                if (Mathf.Abs(u - (cx - 0.09f)) < legW || Mathf.Abs(u - (cx + 0.09f)) < legW)
                {
                    return body;
                }
            }

            // 兜帽 / 披风点缀（右侧飘带）
            if (v < 0.72f && v > 0.50f)
            {
                float dx2 = u - 0.80f;
                float dy2 = v - 0.58f;
                if (dx2 * dx2 + dy2 * dy2 < 0.05f * 0.05f)
                {
                    return accent;
                }
            }

            return clear;
        }
    }
}
