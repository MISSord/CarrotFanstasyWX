using System;

namespace CarrotFantasy
{
	/// <summary>
	/// 全局随机数（UI、表现等非确定性逻辑）。战斗/回放/关卡重开请用
	/// <see cref="DeterministicRandomSession"/>，并在开局时 <see cref="SetSeed"/>，勿依赖 <see cref="ReseedFromTime"/>。
	/// </summary>
	public static class GameRandom
	{
		static SeededRandom rng = new SeededRandom(1);
		static int seed = 1;
		static bool seededExplicitly;

		/// <summary>当前种子（只读）。</summary>
		public static int Seed {
			get { return seed; }
		}

		/// <summary>是否曾调用过 <see cref="SetSeed"/>。</summary>
		public static bool HasExplicitSeed {
			get { return seededExplicitly; }
		}

		static void EnsureInitialized ()
		{
			if (seededExplicitly) {
				return;
			}
			ReseedFromTime();
		}

		/// <summary>使用指定种子重置全局序列（相同种子 → 相同序列）。</summary>
		public static void SetSeed (int newSeed)
		{
			seed = newSeed == 0 ? 1 : newSeed;
			rng = new SeededRandom(seed);
			seededExplicitly = true;
		}

		/// <summary>用 UTC 时间与 GUID 混合值播种（非确定性）。</summary>
		public static void ReseedFromTime ()
		{
			unchecked {
				int mixed = (int)DateTime.UtcNow.Ticks;
				mixed ^= Guid.NewGuid().GetHashCode();
				seed = mixed == 0 ? 1 : mixed;
			}
			rng = new SeededRandom(seed);
			seededExplicitly = false;
		}

		/// <summary>派生子流，不影响全局序列；用于战斗/地图等隔离随机。</summary>
		public static SeededRandom CreateStream (int subSeed)
		{
			unchecked {
				int combined = seed * 397 ^ subSeed;
				return new SeededRandom(combined == 0 ? 1 : combined);
			}
		}

		/// <summary>[minInclusive, maxExclusive) 整数，语义同 Unity Random.Range(int,int)。</summary>
		public static int Range (int minInclusive, int maxExclusive)
		{
			EnsureInitialized();
			return rng.NextInt(minInclusive, maxExclusive);
		}

		/// <summary>[0, 1) 浮点。</summary>
		public static float Value {
			get {
				EnsureInitialized();
				return rng.NextFloat();
			}
		}

		/// <summary>[minInclusive, maxInclusive] 浮点，语义同 Unity Random.Range(float,float)。</summary>
		public static float Range (float minInclusive, float maxInclusive)
		{
			EnsureInitialized();
			return rng.NextFloat(minInclusive, maxInclusive);
		}

		public static bool Chance (float probabilityOne)
		{
			EnsureInitialized();
			return rng.NextBool(probabilityOne);
		}
	}
}
