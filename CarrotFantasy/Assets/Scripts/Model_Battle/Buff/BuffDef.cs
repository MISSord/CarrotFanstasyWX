namespace CarrotFantasy
{
    /// <summary>静态 Buff 配置（由 <see cref="BuffConfigReader"/> 加载）。</summary>
    public class BuffDef
    {
        public int id;
        public BuffCategory category;
        /// <summary>持续时间；0 表示无时长（当前 Phase 1 未使用）。</summary>
        public Fix64 duration;
        /// <summary>DOT 结算间隔；0 表示非周期型。</summary>
        public Fix64 tickInterval;
        /// <summary>DOT 每次扣血。</summary>
        public int tickDamage;
        /// <summary>类别参数，如 Slow 的减速比例（0~1）。</summary>
        public Fix64 param0;
    }
}
