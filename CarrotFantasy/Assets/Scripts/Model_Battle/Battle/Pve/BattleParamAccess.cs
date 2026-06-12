namespace CarrotFantasy
{
    /// <summary>Model 层读取当前开战参数的统一入口（避免散落读取 <see cref="BattleParamServer"/> 平铺字段）。</summary>
    public static class BattleParamAccess
    {
        public static PveModelBattleParams Current =>
            BattleParamServer.Instance != null ? BattleParamServer.Instance.CurrentPveParams : null;

        public static bool HasActivePve => Current != null;

        public static BattlePveMode CurrentMode =>
            Current != null ? Current.Mode : BattlePveMode.Classic;

        /// <summary>开场 UI 与进入 PRE_FIGHTING 的统一时长；计时由 <see cref="PveStartState"/> 驱动。</summary>
        public static float EffectiveStartGameDelaySeconds =>
            Current != null
                ? Current.GetEffectiveStartGameDelaySeconds()
                : PveModelBattleParams.DefaultStartGameDelaySeconds;
    }
}
