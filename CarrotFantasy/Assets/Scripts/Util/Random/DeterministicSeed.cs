namespace CarrotFantasy
{
	/// <summary>
	/// 从业务 id 稳定合成种子（相同输入 → 相同 int，用于开局/重开/回放）。
	/// </summary>
	public static class DeterministicSeed
	{
		public static int Combine (int a, int b)
		{
			unchecked {
				return (a * 397) ^ b;
			}
		}

		public static int Combine (int a, int b, int c)
		{
			return Combine(Combine(a, b), c);
		}

		public static int Combine (int a, int b, int c, int d)
		{
			return Combine(Combine(a, b, c), d);
		}

		/// <summary>
		/// 关卡战斗种子：runSeed 在整局肉鸽开始时生成一次并写入存档；重开/回放使用同一 runSeed。
		/// </summary>
		public static int ForBattle (
			int runSeed,
			int bigLevel,
			int level,
			int encounterId = 0
		)
		{
			unchecked {
				int seed = Combine(runSeed, bigLevel, level, encounterId);
				return seed == 0 ? 1 : seed;
			}
		}

		/// <summary>无 Run 时的单机关卡种子（bigLevel/level 固定则重开一致）。</summary>
		public static int ForClassicLevel (int bigLevel, int level)
		{
			return ForBattle(0x5EED, bigLevel, level, 0);
		}
	}
}
