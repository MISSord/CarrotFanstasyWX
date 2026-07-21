namespace CarrotFantasy
{
    /// <summary>肉鸽单个小关进度（跨 Run 持久）。</summary>
    public class RoguelikeSingleLevelInfo
    {
        public byte bigLevelId;
        public byte levelId;

        /// <summary><see cref="RoguelikeMapInfoType.CLEARED"/> / <see cref="RoguelikeMapInfoType.NOT_CLEARED"/>。</summary>
        public byte cleared;

        /// <summary><see cref="RoguelikeMapInfoType.UNLOCK_LEVEL"/> / <see cref="RoguelikeMapInfoType.LOCK_LEVEL"/>。</summary>
        public byte unlocked;
    }

    public class RoguelikeBigLevelInfo
    {
        public int bigLevel;
        public int count;
        public int unlockCount;
        public bool isLock;
    }
}
