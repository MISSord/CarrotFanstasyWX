using System;

namespace CarrotFantasy
{
    public class BattleEvent
    {
        public const String START_GAME = "Start_game";
        public const String END_GAME = "end_game";

        public const String AFTER_TICK = "after_tick";

        public const String ITEM_BUILD = "Item_build";
        public const String ITEM_DESTROY = "Item_destroy";

        public const String BATTLE_FINISH = "battle_finish";
        public const String ON_BATTLE_PAUSE = "on_battle_pause";
        public const String ON_BATTLE_RESUME = "on_battle_resume";

        //public const String COIN_ADD = "Coin_Add";
        //public const String COIN_REDUCE = "Coin_Reduce";
        public const String COIN_CHANGE = "Coin_Change";

        public const String WAVES_NUMBER_ADD = "Waves_Number_Add";

        public const String CARROT_LIVE_REDUCE = "Carrot_Live_reduce";

        public const String MONSTER_DIED = "Monster_Died";
        public const String MONSTER_LIVE_REDUCE = "Monster_Live_Reduce";

        public const String BATTLE_UNIT_ADD = "Battle_Unit_Add";
        public const String BATTLE_UNIT_REMOVE = "Battle_Unit_Remove";

        /// <summary>表现层（兼容旧监听）；PVE 结算请改用 <see cref="BattleCoreEvent.PVE_MATCH_SETTLED"/>。</summary>
        public const String SHOW_GAME_FINISH_PAGE = "Show_Game_Finish_Page";
        /// <summary>表现层（兼容旧监听）；PVE 结算请改用 <see cref="BattleCoreEvent.PVE_MATCH_SETTLED"/>。</summary>
        public const String SHOW_GAME_OVER_PAGE = "Show_Game_Over_Page";

        public const String REPLAY_THE_GAME = "Replay_The_Game";
        public const String PAUSE_THE_GAME = "Pause_The_Game";
        public const String GO_ON_GAME = "Go_On_Game";

        public const String TOWER_LEVEL_UP = "Tower_Level_Up";
        public const String TOWER_ATTACK = "Tower_Attack";

        public const String BULLET_BUILD = "Bullet_Build";
        public const String BULLET_REMOVE = "Bullet_Remove";

        public const String TOWER_RANGE_SHOW = "Tower_Range_Show";
        public const String TOWER_RANGE_FADE = "Tower_Range_Fade";

        public const String ITEM_DIED = "Item_Died";
        public const String ITEM_LIVE_REDUCE = "Item_Live_Reduce";

        public const String TARGET_CHANGE = "Target_Change";

        public const String GAME_STATE_CHANGE = "Game_State_Change";
    }

    public class UnitEvent
    {
        public const String POSITION_CHANGE = "position_change";
        public const String ROTATION_CHANGE = "rotation_change";
        public const String STATUS_CHANGE = "status_change";
        public const String FACE_DIRECTION_CHANGE = "face_direction_change"; // 怪物脸的方向
        public const String TOWER_DIRECTION_CHANGE = "tower_direction_change"; //炮台的方向
        public const String BODY_RECT_CHANE = "body_rect_change";

        public const String TOWER_UPDATE = "tower_update";

        public const String DAMAGE_CALCULATE_COMPLETE = "damage_calculate_complete";

        public const String KILL_UNIT = "kill_unit";

        public const String HP_CHANGE = "hp_change";
    }
}
