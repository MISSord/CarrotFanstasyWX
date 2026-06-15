namespace CarrotFantasy
{
    /// <summary>
    /// 开战参数读取入口。优先读当前 Session 内 <see cref="BaseBattle.LaunchParams"/>；
    /// Session 尚未创建时回退 <see cref="BattleParamServer.CurrentPveParams"/>（进关过渡期）。
    /// </summary>
    public static class BattleParamAccess
    {
        public static PveModelBattleParams Current
        {
            get
            {
                PveModelBattleParams fromBattle = ServerProvision.battleSessionHost?.baseBattle?.LaunchParams;
                if (fromBattle != null)
                {
                    return fromBattle;
                }

                return BattleParamServer.Instance != null
                    ? BattleParamServer.Instance.CurrentPveParams
                    : null;
            }
        }

        public static bool HasActivePve => Current != null;

        public static BattlePveMode CurrentMode =>
            Current != null ? Current.Mode : BattlePveMode.Classic;

        /// <summary>开场 UI 与进入 PRE_FIGHTING 的统一时长；计时由 <see cref="PveStartState"/> 驱动。</summary>
        public static float EffectiveStartGameDelaySeconds =>
            Current != null
                ? Current.GetEffectiveStartGameDelaySeconds()
                : PveModelBattleParams.DefaultStartGameDelaySeconds;

        public static float GetEffectiveStartGameDelaySeconds(BaseBattle battle)
        {
            if (battle?.LaunchParams != null)
            {
                return battle.LaunchParams.GetEffectiveStartGameDelaySeconds();
            }

            return EffectiveStartGameDelaySeconds;
        }
    }
}
