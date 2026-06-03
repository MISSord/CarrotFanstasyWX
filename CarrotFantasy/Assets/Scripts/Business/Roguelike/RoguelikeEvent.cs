namespace CarrotFantasy
{
    public static class RoguelikeEvent
    {
        public const string RUN_STARTED = "Roguelike_Run_Started";
        public const string RUN_ENDED = "Roguelike_Run_Ended";
        public const string GOLD_CHANGED = "Roguelike_Gold_Changed";
        public const string INVENTORY_CHANGED = "Roguelike_Inventory_Changed";
        public const string ITEM_PURCHASED = "Roguelike_Item_Purchased";
        public const string RETURN_TO_MAP_REQUESTED = "Roguelike_Return_To_Map";
    }

    public enum RoguelikeGoldSource
    {
        Unknown = 0,
        BattleVictory = 1,
        RandomEvent = 2,
        Debug = 3,
    }

    public enum RoguelikeRunEndReason
    {
        Victory = 0,
        Defeat = 1,
        Abandoned = 2,
    }
}
