namespace CarrotFantasy
{
    /// <summary>怪物伤害结算（子弹直伤、Buff DOT 等统一入口）。</summary>
    public static class BattleMonsterDamage
    {
        public static bool TryResolveBulletHit(BattleUnit_Monster monster, BattleUnit_Bullet bullet)
        {
            if (monster == null || bullet == null)
            {
                return false;
            }

            if (monster.IsDamageImmune())
            {
                return false;
            }

            if (monster.HasBeenHitByBullet(bullet.uid))
            {
                return false;
            }

            monster.RegisterBulletHit(bullet.uid);
            ApplyDamage(monster, bullet.damage);
            TryApplyBulletBuff(monster, bullet);
            return true;
        }

        public static void ApplyDamage(BattleUnit_Monster monster, int damage)
        {
            if (monster == null || damage <= 0)
            {
                return;
            }

            if (monster.IsDamageImmune())
            {
                return;
            }

            monster.curLive -= damage;
            monster.eventDipatcher.DispatchEvent<int>(BattleEvent.MONSTER_DAMAGE_NUMBER, damage);
            monster.eventDipatcher.DispatchEvent<int>(UnitEvent.DAMAGE_CALCULATE_COMPLETE, damage);
            monster.eventDipatcher.DispatchEvent(BattleEvent.MONSTER_LIVE_REDUCE);
            monster.TryMarkDeadFromDamage();
        }

        private static void TryApplyBulletBuff(BattleUnit_Monster monster, BattleUnit_Bullet bullet)
        {
            int buffId = bullet.onHitBuffId;
            if (buffId <= 0)
            {
                return;
            }

            UnitBuffComponent buff = monster.GetBuffComponent();
            if (buff == null)
            {
                return;
            }

            buff.ApplyBuff(buffId, BuffApplyContext.FromSource(bullet.uid));
        }
    }
}
