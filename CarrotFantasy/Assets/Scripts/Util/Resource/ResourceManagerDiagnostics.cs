using System.Collections.Generic;
using System.Text;

namespace CarrotFantasy
{
    /// <summary>
    /// 资源管理器职责约定与聚合诊断。
    /// <para>
    /// 选用约定：
    /// <list type="bullet">
    /// <item><see cref="PrefabResourceManager"/>：有感 Load/Unload 模板（面板 ViewLoader、预加载、长驻 Tip）</item>
    /// <item><see cref="GameObjectResourceManager"/>：有感 Load/Unload 实例（特效、临时物；内部走 PrefabRM）</item>
    /// <item><see cref="AtlasResourceManager"/> + UIImageLoader/SpriteLoader：图集 Sprite（无感引用，整图集进内存后卸 AB）</item>
    /// <item><see cref="SpriteResourceManager"/> / <see cref="TextureResourceManager"/>：非图集零散资源</item>
    /// </list>
    /// Atlas 与 Prefab 加载模型不同（整图集 vs 单模板），故不抽共用 RefCounted 缓存，只统一诊断形态。
    /// </para>
    /// </summary>
    public static class ResourceManagerDiagnostics
    {
        const string LogModule = "ResourceDiagnostics";

        public static void CollectAll(List<ResourceUsageSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            AtlasResourceManager.Instance.CollectSnapshots(into);
            PrefabResourceManager.Instance.CollectSnapshots(into);
            GameObjectResourceManager.Instance.CollectSnapshots(into);
        }

        public static List<ResourceUsageSnapshot> CaptureAll()
        {
            var list = new List<ResourceUsageSnapshot>(64);
            CollectAll(list);
            return list;
        }

        public static void DumpAll(string reason = null)
        {
            string prefix = string.IsNullOrEmpty(reason) ? "" : "[" + reason + "] ";
            List<ResourceUsageSnapshot> list = CaptureAll();
            if (list.Count == 0)
            {
                GameLogController.Log(prefix + "资源快照为空（无缓存/无持有）", LogModule);
            }
            else
            {
                var sb = new StringBuilder(512);
                sb.Append(prefix).Append("资源快照共 ").Append(list.Count).Append(" 条：");
                GameLogController.Log(sb.ToString(), LogModule);
                for (int i = 0; i < list.Count; i++)
                {
                    ResourceUsageSnapshot s = list[i];
                    GameLogController.Log(
                        $"  [{s.Manager}] {s.Key} ref={s.RefCount} cached={s.HasCachedObject} loading={s.IsLoading} resident={s.IsResident} {s.Detail}",
                        LogModule);
                }
            }

            PrefabResourceManager.Instance.DumpAliveHandles(reason);
            GameObjectResourceManager.Instance.DumpAliveHandles(reason);
        }

        public static void WarnLeaks()
        {
            PrefabResourceManager.Instance.WarnLeakedHandles();
            GameObjectResourceManager.Instance.WarnLeakedHandles();
        }
    }
}
