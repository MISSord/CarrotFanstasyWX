using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>按 Tower.xlsx 的 BulletMoveType 创建追踪/直线等移动组件。</summary>
    public static class BulletMoveComponentFactory
    {
        public static BulletMoveType Normalize(BulletMoveType moveType)
        {
            if (moveType == BulletMoveType.None)
            {
                return BulletMoveType.Homing;
            }

            return moveType;
        }

        public static BulletMoveType Parse(Fix64 raw)
        {
            return Normalize((BulletMoveType)(int)raw);
        }

        public static UnitMoveComponent_Bullet CreateFromBirthParam(Dictionary<string, Fix64> birthParam)
        {
            if (birthParam != null && birthParam.TryGetValue("bulletMoveType", out Fix64 raw))
            {
                return Create(Parse(raw));
            }

            return Create(BulletMoveType.Homing);
        }

        public static UnitMoveComponent_Bullet Create(BulletMoveType moveType)
        {
            switch (Normalize(moveType))
            {
                case BulletMoveType.Straight:
                    return Acquire<UnitMoveComponent_Bullet_One>(UnitComponentType.MOVE_BULLET_ONE)
                           ?? new UnitMoveComponent_Bullet_One();
                case BulletMoveType.Homing:
                    return Acquire<UnitMoveComponent_Bullet>(UnitComponentType.MOVE_BULLET)
                           ?? new UnitMoveComponent_Bullet();
                default:
                    Debug.LogWarning("Unsupported BulletMoveType " + moveType + ", fallback to Homing.");
                    return Acquire<UnitMoveComponent_Bullet>(UnitComponentType.MOVE_BULLET)
                           ?? new UnitMoveComponent_Bullet();
            }
        }

        static T Acquire<T>(string componentType) where T : BaseUnitComponent
        {
            return BattleUnitPool.Instance.GetNewUnitComponent<T>(componentType);
        }
    }
}
