using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// <see cref="Image"/> 的便捷扩展。
    /// <para>
    /// 调用方不需要知道 <see cref="UIImageLoader"/> 是否存在——缺失时这里会自动挂一个
    /// （"无感存在"），加载、换图与引用释放都由该组件负责，业务只关心包名和精灵名。
    /// </para>
    /// <para>
    /// 注意：运行时补挂的组件不会被写回预制体。需要美术/策划在编辑器里可视化配置的图片，
    /// 仍应在预制体上手动挂 <see cref="UIImageLoader"/> 并填好包名/资源名。
    /// </para>
    /// </summary>
    public static class ImageExtensions
    {

        /// <summary>从图集取图；<paramref name="bundleName"/> 为图集 AB 包名。业务无需处理引用计数。</summary>
        public static void SetAtlasSprite(this Image image, string bundleName, string spriteName, LoadPriority priority = LoadPriority.Medium)
        {
            if (image == null)
            {
                return;
            }

            EnsureLoader(image).SetAtlasSprite(bundleName, spriteName, priority);
        }

        /// <summary>拿现有 loader，没有就补一个——调用方不必关心它存不存在。</summary>
        static UIImageLoader EnsureLoader(Image image)
        {
            UIImageLoader loader = image.GetComponent<UIImageLoader>();
            if (loader == null)
            {
                loader = image.gameObject.AddComponent<UIImageLoader>();
            }

            return loader;
        }
    }
}
