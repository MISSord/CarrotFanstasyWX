using System;

namespace CarrotFantasy
{
    public static class MapInfoType
    {
        public const byte UNLOCK_LEVEL = 1;
        public const byte LOCK_LEVEL = 2;

        public const byte CARROT_STATE_NORMAL = 1;
        public const byte CARROT_STATE_SLIVER = 2;
        public const byte CARROT_STATE_GOLD = 3;

        public const byte ALL_CLEAR = 1;
        public const byte NOT_ALL_CLEAR = 2;
    }

    public class MapEventType
    {
        public const String MAP_INFO_CHANGE = "Map_Info_Change";
        public const String CAN_START_GAME = "Can_Start_Game";
        public const String CANT_START_GAME = "Cant_Start_Game";
    }
}
