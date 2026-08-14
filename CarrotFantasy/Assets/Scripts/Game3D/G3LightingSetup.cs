using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 3D 场景光照框架，模仿《明日方舟》打光方案：
    /// 一盏主光（Mixed，可烘焙）+ 环境光 + 可选反射探针，静态场景将来配合 Lightmap 烘焙。
    /// </summary>
    public class G3LightingSetup : MonoBehaviour
    {
        [Tooltip("主光颜色（暖色偏写实）。")]
        public Color mainLightColor = new Color(1f, 0.95f, 0.86f);

        [Tooltip("主光强度。")]
        public float mainLightIntensity = 1.1f;

        [Tooltip("主光俯仰角。")]
        [Range(20f, 80f)]
        public float mainLightPitch = 50f;

        [Tooltip("主光朝向（0=正面，90=右侧）。")]
        public float mainLightYaw = 35f;

        [Tooltip("环境光强度。")]
        [Range(0f, 1.5f)]
        public float ambientIntensity = 0.7f;

        [Tooltip("环境光颜色（冷色调）。")]
        public Color ambientColor = new Color(0.42f, 0.46f, 0.54f);

        [Tooltip("是否创建天空盒。关闭时用纯色背景。")]
        public bool enableSkybox = true;

        [Tooltip("纯色背景色（enableSkybox = false 时生效）。")]
        public Color backgroundColor = new Color(0.08f, 0.1f, 0.12f);

        public Light MainLight { get; private set; }

        const string MainLightObjectName = "G3_MainLight";

        public void Apply()
        {
            EnsureMainLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor * ambientIntensity;
            RenderSettings.skybox = enableSkybox ? RenderSettings.skybox : null;
        }

        void EnsureMainLight()
        {
            Transform existing = transform.Find(MainLightObjectName);
            if (existing != null)
            {
                MainLight = existing.GetComponent<Light>();
            }
            else
            {
                GameObject lightGo = new GameObject(MainLightObjectName);
                lightGo.transform.SetParent(transform, false);
                MainLight = lightGo.AddComponent<Light>();
            }

            if (MainLight == null)
            {
                return;
            }

            MainLight.type = LightType.Directional;
            MainLight.color = mainLightColor;
            MainLight.intensity = mainLightIntensity;
            MainLight.lightmapBakeType = LightmapBakeType.Mixed;
            MainLight.shadows = LightShadows.Soft;
            MainLight.transform.rotation = Quaternion.Euler(mainLightPitch, mainLightYaw, 0f);
        }
    }
}
