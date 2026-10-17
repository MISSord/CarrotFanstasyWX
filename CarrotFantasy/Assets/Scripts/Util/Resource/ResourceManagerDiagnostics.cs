using System.Collections.Generic;
using System.Text;

namespace CarrotFantasy
{
    /// <summary>
    /// 资源管理器职责约定与聚合诊断。
    /// <para>
    /// 选用约定：
    /// <list type="bullet">
    /// <item><see cref="PrefabLoader"/> / <see cref="ImageLoader"/>：单资源句柄，调用方 Release 归还。</item>
    /// <item><see cref="AtlasLoader"/>：图集整包加载与精灵引用计数。</item>
    /// </list>
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

            AtlasLoader.Instance.CollectSnapshots(into);
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
        }
    }
}
