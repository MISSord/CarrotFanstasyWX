namespace CarrotFantasy
{
    public class BuffEventPayload
    {
        public int buffId;
        public BuffCategory category;
        public Fix64 remainingTime;
        public int sourceUid;
        public bool isRefresh;

        public static BuffEventPayload FromInstance(BuffInstance inst, bool isRefresh)
        {
            return new BuffEventPayload
            {
                buffId = inst.buffId,
                category = inst.category,
                remainingTime = inst.remainingTime,
                sourceUid = inst.sourceUid,
                isRefresh = isRefresh,
            };
        }
    }
}
