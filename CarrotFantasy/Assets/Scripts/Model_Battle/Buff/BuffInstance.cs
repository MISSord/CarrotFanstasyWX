namespace CarrotFantasy
{
    /// <summary>单位身上运行中的 Buff（同 buffId 仅一条，不叠层）。</summary>
    public class BuffInstance
    {
        public int buffId;
        public BuffCategory category;
        public int sourceUid;
        public Fix64 remainingTime;
        public Fix64 tickInterval;
        public int tickDamage;
        public Fix64 param0;
        public Fix64 nextTickTime;
    }
}
