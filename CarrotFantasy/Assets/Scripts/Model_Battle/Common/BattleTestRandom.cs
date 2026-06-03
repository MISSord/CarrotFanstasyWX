namespace CarrotFantasy
{
	/// <summary>确定性伪随机（碰撞/刷怪测试用），内部使用 <see cref="SeededRandom"/>。</summary>
	public class BattleTestRandom
	{
		readonly SeededRandom inner;

		public BattleTestRandom (uint seed)
		{
			inner = new SeededRandom(seed);
		}

		public int NextInt (int minInclusive, int maxExclusive)
		{
			return inner.NextInt(minInclusive, maxExclusive);
		}
	}
}
