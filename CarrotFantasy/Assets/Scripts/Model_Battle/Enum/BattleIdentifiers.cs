using System;

namespace CarrotFantasy
{
    public class BattleUnitType
    {
        public const String TOWER = "TOWER";
        public const String MONSTER = "MONSTER";
        /// <summary>流场新模式怪物，与 <see cref="MONSTER"/> 分池。</summary>
        public const String MONSTER_FLOW = "MONSTER_FLOW";
        public const String ITEM = "ITEM";
        public const String BULLET = "BULLET";
    }

    public class BattleStateType
    {
        public const String START_GAME = "start_game";
        public const String FIGHTINT = "fighting";
        public const String PRE_FIGHTINT = "pre_fighting";
        public const String END_GAME = "end_game";
    }

    public class InputOrderType
    {
        public const int UPDATE_ORDER = 0;
        public const int ADD_ORDER = 1;
        public const int REMOVE_ORDER = 2;
    }
}
