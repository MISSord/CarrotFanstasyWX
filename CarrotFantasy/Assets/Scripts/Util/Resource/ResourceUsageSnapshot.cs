using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>
    /// 资源管理器统一诊断条目（Atlas / Prefab / GameObject）。
    /// </summary>
    public struct ResourceUsageSnapshot
    {
        /// <summary>管理器名，如 Atlas / Prefab / GameObject。</summary>
        public string Manager;

        /// <summary>逻辑键：图集为 bundleName；其它为 bundle|asset。</summary>
        public string Key;

        /// <summary>逻辑引用数（handle/token 持有数）。</summary>
        public int RefCount;

        /// <summary>是否已有可用对象在内存。</summary>
        public bool HasCachedObject;

        /// <summary>是否正在加载。</summary>
        public bool IsLoading;

        /// <summary>是否常驻（仅 Atlas 等有意义）。</summary>
        public bool IsResident;

        /// <summary>附加说明（状态、sprite 数、实例存活等）。</summary>
        public string Detail;
    }

    /// <summary>可提供诊断快照的资源管理器。</summary>
    public interface IResourceDiagnostics
    {
        string DiagnosticsName { get; }

        void CollectSnapshots(List<ResourceUsageSnapshot> into);

        void DumpAliveHandles(string reason = null);

        void WarnLeakedHandles();
    }
}
