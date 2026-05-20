namespace CarrotFantasy
{
    /// <summary>确定性伪随机（碰撞/刷怪测试用）。</summary>
    public class BattleTestRandom
    {
        private uint state;

        public BattleTestRandom(uint seed)
        {
            this.state = seed != 0 ? seed : 1u;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(this.NextUInt() % range);
        }

        private uint NextUInt()
        {
            this.state = this.state * 1664525u + 1013904223u;
            return this.state;
        }
    }
}
