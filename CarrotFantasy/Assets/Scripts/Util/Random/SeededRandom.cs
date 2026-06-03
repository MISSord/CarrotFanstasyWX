namespace CarrotFantasy
{
	/// <summary>
	/// 可复现的线性同余伪随机发生器（与旧版 BattleTestRandom 算法一致）。
	/// </summary>
	public sealed class SeededRandom
	{
		uint state;

		public SeededRandom (int seed)
		{
			state = (uint)seed;
			if (state == 0) {
				state = 1u;
			}
		}

		public SeededRandom (uint seed)
		{
			state = seed != 0 ? seed : 1u;
		}

		public int NextInt (int minInclusive, int maxExclusive)
		{
			if (maxExclusive <= minInclusive) {
				return minInclusive;
			}

			uint range = (uint)(maxExclusive - minInclusive);
			return minInclusive + (int)(NextUInt() % range);
		}

		/// <summary>返回 [0, 1) 的 float。</summary>
		public float NextFloat ()
		{
			return (NextUInt() & 0x00FFFFFFu) / 16777216f;
		}

		public float NextFloat (float minInclusive, float maxExclusive)
		{
			return minInclusive + (maxExclusive - minInclusive) * NextFloat();
		}

		public bool NextBool (float probabilityOne = 0.5f)
		{
			return NextFloat() < probabilityOne;
		}

		uint NextUInt ()
		{
			state = state * 1664525u + 1013904223u;
			return state;
		}
	}
}
