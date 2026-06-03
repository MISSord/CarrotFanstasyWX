namespace CarrotFantasy
{
	/// <summary>
	/// 可复现随机会话：在「首次开局 / 关卡重开 / 回放开始」时调用 <see cref="Reset"/>，
	/// 会话内按相同顺序取数则得到相同结果。种子须来自存档或 <see cref="DeterministicSeed"/>，不要用时间播种。
	/// </summary>
	public sealed class DeterministicRandomSession
	{
		readonly int rootSeed;
		SeededRandom main;

		public DeterministicRandomSession (int rootSeed)
		{
			this.rootSeed = rootSeed == 0 ? 1 : rootSeed;
			Reset();
		}

		public int RootSeed {
			get { return rootSeed; }
		}

		/// <summary>将序列指针回到起点（重开、回放加载后必须调用）。</summary>
		public void Reset ()
		{
			main = new SeededRandom(rootSeed);
		}

		public int Range (int minInclusive, int maxExclusive)
		{
			return main.NextInt(minInclusive, maxExclusive);
		}

		public float Value {
			get { return main.NextFloat(); }
		}

		public float Range (float minInclusive, float maxInclusive)
		{
			return main.NextFloat(minInclusive, maxInclusive);
		}

		public bool Chance (float probabilityOne)
		{
			return main.NextBool(probabilityOne);
		}

		/// <summary>
		/// 命名子流：与主序列独立计数，但由 rootSeed 派生，适合「刷怪 / 暴击」等分区而不互相抢次数。
		/// </summary>
		public SeededRandom CreateStream (int streamId)
		{
			unchecked {
				int sub = DeterministicSeed.Combine(rootSeed, streamId);
				return new SeededRandom(sub);
			}
		}
	}
}
