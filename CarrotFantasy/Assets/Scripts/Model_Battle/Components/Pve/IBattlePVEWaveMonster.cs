namespace CarrotFantasy
{
    /// <summary>
    /// PVE 波次刷怪组件对外接口，供 <see cref="PveStateMachine"/> 各状态使用。
    /// 由 <see cref="BattlePVEMonsterComponent"/> 实现。
    /// </summary>
    public interface IBattlePVEWaveMonster
    {
        void BuildNewWavesMonster();

        bool IsCanNewMonsterWaves();
    }

    public static class BattlePVEWaveMonster
    {
        public static IBattlePVEWaveMonster GetFrom(BaseBattle battle)
        {
            if (battle == null)
            {
                return null;
            }

            return battle.GetComponent(BattleComponentType.MonsterComponent) as IBattlePVEWaveMonster;
        }
    }
}
