using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// <see cref="RawImage"/> 的便捷扩展。
    /// <para>
    /// 调用方不需要知道 <see cref="RawImageLoader"/> 是否存在——缺失时这里会自动挂一个
    /// （"无感存在"），加载、换图与引用释放都由该组件负责，业务只关心包名和资源名。
    /// </para>
    /// <para>
    /// 单张贴图刻意不走 <see cref="UIImageLoader"/>：后者面向图集精灵，
    /// 两者混在一起会让引用计数和释放时机互相干扰。
    /// </para>
    /// <para>
    /// 注意：运行时补挂的组件不会被写回预制体。需要美术/策划在编辑器里可视化配置的 RawImage，
    /// 仍应在预制体上手动挂 <see cref="RawImageLoader"/> 并填好包名/资源名。
    /// </para>
    /// </summary>
    public static class RawImageExtensions
    {
        /// <summary>按单张贴图取图；<paramref name="bundleName"/> 为图片自身的 AB 包名（常见为 ui/rawimages/xxx）。</summary>
        public static void SetTexture(this RawImage rawImage, string bundleName, string assetName, LoadPriority priority = LoadPriority.Medium)
        {
            if (rawImage == null)
            {
                return;
            }

            EnsureLoader(rawImage).SetTexture(bundleName, assetName, priority);
        }

        /// <summary>拿现有 loader，没有就补一个——调用方不必关心它存不存在。</summary>
        static RawImageLoader EnsureLoader(RawImage rawImage)
        {
            RawImageLoader loader = rawImage.GetComponent<RawImageLoader>();
            if (loader == null)
            {
                loader = rawImage.gameObject.AddComponent<RawImageLoader>();
            }

            return loader;
        }
    }
}
