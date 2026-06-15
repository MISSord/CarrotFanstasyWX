namespace CarrotFantasy
{
    /// <summary>PVE 结算表现与存档提交；与 Session 流水线解耦。</summary>
    public static class BattleSettlementPresenter
    {
        public static void Handle(BaseBattle battle, PveMatchSettlement settlement)
        {
            if (settlement == null)
            {
                return;
            }

            BattlePveMode mode = battle?.LaunchParams != null
                ? battle.LaunchParams.Mode
                : BattlePveMode.Classic;

            if (mode == BattlePveMode.Roguelike && RoguelikeRunManager.Instance != null)
            {
                RoguelikeRunManager.Instance.HandlePveMatchSettled(settlement);
                return;
            }

            if (settlement.IsVictory && settlement.VictoryProgress != null && battle?.HostBridge != null)
            {
                battle.HostBridge.SubmitVictoryMapProgress(settlement.VictoryProgress);
            }

            if (settlement.IsVictory)
            {
                ShowGameWin(battle);
            }
            else
            {
                ShowGameOver(battle);
            }
        }

        static void ShowGameWin(BaseBattle battle)
        {
            if (battle != null)
            {
                BattleViewOpener.Open<GameWinView>(battle);
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Perfect");
        }

        static void ShowGameOver(BaseBattle battle)
        {
            if (battle != null)
            {
                BattleViewOpener.Open<GameOverView>(battle);
            }

            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Lose");
        }
    }
}
