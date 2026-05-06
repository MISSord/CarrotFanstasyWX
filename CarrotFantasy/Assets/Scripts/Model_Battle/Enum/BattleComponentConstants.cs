using System;

namespace CarrotFantasy
{
    public class BattleComponentType
    {
        public const String MapComponent = "MapComponent"; //地图
        public const String DataComponent = "DataComponent"; //信息
        public const String LogComponent = "LogComponent";//
        public const String InputComponent = "InputComponent"; //输入管理 
        public const String RandomComponent = "RandomComponent";//随机数
        //public const String RecordComponent = "RecordComponent"; //成绩

        public const String SchedulerComponent = "SchedulerComponent"; //延迟调用
        public const String HitTestComponent = "HitTestComponent"; //碰撞检测
        public const String JudgeComponent = "JudgeComponent"; //判断游戏过程

        public const String TowerComponent = "TowerComponent";
        public const String MonsterComponent = "MonsterComponent";
        public const String BulletComponent = "BulletComponent";
        public const String ItemComponent = "ItemComponent";
    }

    public class UnitComponentType
    {
        public const String TRANSFORM = "Transform"; //坐标
        public const String MOVE = "Move"; //移动
        public const String ACTION = "Action";
        public const String BEHIT = "Behit"; //碰撞

        public const String SKILL = "Skill";
        public const String TRAIN = "Train"; //传输

        public const String STATUS = "STATUS";//状态

        public const String MOVE_MONSTER = "Move_Monster";
        public const String MOVE_BULLET = "Move_Bullet";
        public const String MOVE_BULLET_ONE = "Move_Bullet_One";
    }
}
