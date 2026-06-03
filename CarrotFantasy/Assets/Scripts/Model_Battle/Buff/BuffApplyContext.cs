namespace CarrotFantasy
{
    public struct BuffApplyContext
    {
        public int sourceUid;

        public static BuffApplyContext FromSource(int sourceUid)
        {
            return new BuffApplyContext { sourceUid = sourceUid };
        }
    }
}
