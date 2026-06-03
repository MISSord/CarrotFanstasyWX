namespace CarrotFantasy
{
    internal static class MonsterLocomotionUtil
    {
        public static Fix64 GetSpeedMultiplier(BattleUnit unit)
        {
            BattleUnit_Monster monster = unit as BattleUnit_Monster;
            if (monster == null)
            {
                return Fix64.One;
            }

            UnitBuffComponent buff = monster.GetBuffComponent();
            if (buff == null)
            {
                return Fix64.One;
            }

            return buff.GetSpeedMultiplier();
        }
    }
}
