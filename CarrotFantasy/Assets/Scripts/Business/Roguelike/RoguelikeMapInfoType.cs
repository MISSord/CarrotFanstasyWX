using System;

namespace CarrotFantasy
{
    public static class RoguelikeMapInfoType
    {
        public const byte UNLOCK_LEVEL = 1;
        public const byte LOCK_LEVEL = 2;

        public const byte CLEARED = 1;
        public const byte NOT_CLEARED = 2;
    }

    public static class RoguelikeMapEventType
    {
        public const String MAP_INFO_CHANGE = "Roguelike_Map_Info_Change";
        public const String CAN_ENTER_LEVEL = "Roguelike_Can_Enter_Level";
        public const String CANT_ENTER_LEVEL = "Roguelike_Cant_Enter_Level";
    }
}
