namespace CarrotFantasy
{
    public static class BattleGlobalBuffCompiler
    {
        public static BattleGlobalBuffSnapshot Compile(PveModelBattleParams launchParams)
        {
            var snapshot = new BattleGlobalBuffSnapshot();
            if (launchParams == null)
            {
                return snapshot;
            }

            snapshot.StartCoinBonus = launchParams.StartCoinBonus;
            snapshot.TowerDamagePercentBonus = launchParams.TowerDamagePercentBonus;

            if (launchParams.GlobalBuffIds == null || launchParams.GlobalBuffIds.Count == 0)
            {
                return snapshot;
            }

            for (int i = 0; i < launchParams.GlobalBuffIds.Count; i++)
            {
                int buffId = launchParams.GlobalBuffIds[i];
                if (buffId <= 0)
                {
                    continue;
                }

                if (!BuffConfigReader.Instance.TryGetDef(buffId, out BuffDef def))
                {
                    continue;
                }

                switch (def.category)
                {
                    case BuffCategory.DamageAmp:
                        snapshot.TowerDamagePercentBonus += ToPercentBonus(def.param0);
                        break;
                }
            }

            return snapshot;
        }

        private static int ToPercentBonus(Fix64 param0)
        {
            float value = (float)param0;
            if (value >= 1f)
            {
                return (int)value;
            }

            return (int)(value * 100f);
        }
    }
}
