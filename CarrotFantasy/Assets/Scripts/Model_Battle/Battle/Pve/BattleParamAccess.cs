namespace CarrotFantasy
{
    /// <summary>
    /// 开战参数读取入口。仅从 <see cref="BaseBattle.LaunchParams"/> 读取（Session 注入后唯一来源）。
    /// </summary>
    public static class BattleParamAccess
    {
        public static float GetEffectiveStartGameDelaySeconds(BaseBattle battle)
        {
            if (battle?.LaunchParams != null)
            {
                return battle.LaunchParams.GetEffectiveStartGameDelaySeconds();
            }

            return PveModelBattleParams.DefaultStartGameDelaySeconds;
        }
    }
}
