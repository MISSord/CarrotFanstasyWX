using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>开战参数快照；不含 Unity 场景引用（场景壳由 <see cref="BattleSessionHost"/> 持有）。</summary>
    public sealed class BattleSessionConfig
    {
        public PveModelBattleParams Params { get; }
        public int BattleRandomSeed { get; }

        BattleSessionConfig(PveModelBattleParams launchParams, int battleRandomSeed)
        {
            this.Params = launchParams;
            this.BattleRandomSeed = battleRandomSeed;
        }

        public static BattleSessionConfig Create(PveModelBattleParams launchParams)
        {
            if (launchParams == null)
            {
                return null;
            }

            int seed = launchParams.BattleRandomSeed;
            return new BattleSessionConfig(launchParams, seed);
        }
    }
}
