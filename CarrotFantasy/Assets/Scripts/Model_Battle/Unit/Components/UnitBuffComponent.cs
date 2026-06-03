using System.Collections.Generic;

namespace CarrotFantasy
{
    public class UnitBuffComponent : BaseUnitComponent
    {
        private readonly Dictionary<int, BuffInstance> activeBuffs = new Dictionary<int, BuffInstance>();
        private readonly List<int> expiredBuffIds = new List<int>();

        public UnitBuffComponent()
        {
            this.unitComponentType = UnitComponentType.BUFF;
        }

        public bool HasBuff(int buffId)
        {
            return this.activeBuffs.ContainsKey(buffId);
        }

        public bool TryGetBuff(int buffId, out BuffInstance instance)
        {
            return this.activeBuffs.TryGetValue(buffId, out instance);
        }

        /// <summary>同 buffId 仅刷新剩余时间，不叠层。</summary>
        public bool ApplyBuff(int buffId, BuffApplyContext ctx)
        {
            if (this.unit == null || buffId <= 0)
            {
                return false;
            }

            if (!BuffConfigReader.Instance.TryGetDef(buffId, out BuffDef def))
            {
                return false;
            }

            Fix64 clock = this.unit.baseBattle.curClock;

            if (this.activeBuffs.TryGetValue(buffId, out BuffInstance existing))
            {
                existing.remainingTime = def.duration;
                existing.sourceUid = ctx.sourceUid;
                existing.nextTickTime = ComputeNextTickTime(clock, def);
                this.unit.eventDipatcher.DispatchEvent<BuffEventPayload>(
                    UnitEvent.BUFF_REFRESH,
                    BuffEventPayload.FromInstance(existing, true));
                this.NotifyStatusChangeIfNeeded(def.category);
                return true;
            }

            BuffInstance inst = new BuffInstance
            {
                buffId = def.id,
                category = def.category,
                sourceUid = ctx.sourceUid,
                remainingTime = def.duration,
                tickInterval = def.tickInterval,
                tickDamage = def.tickDamage,
                param0 = def.param0,
                nextTickTime = ComputeNextTickTime(clock, def),
            };
            this.activeBuffs.Add(buffId, inst);
            this.unit.eventDipatcher.DispatchEvent<BuffEventPayload>(
                UnitEvent.BUFF_ADD,
                BuffEventPayload.FromInstance(inst, false));
            this.NotifyStatusChangeIfNeeded(def.category);
            return true;
        }

        public bool RemoveBuff(int buffId)
        {
            if (!this.activeBuffs.TryGetValue(buffId, out BuffInstance inst))
            {
                return false;
            }

            BuffCategory category = inst.category;
            this.activeBuffs.Remove(buffId);
            this.DispatchRemove(inst);
            this.NotifyStatusChangeIfNeeded(category);
            return true;
        }

        public void ClearAllBuffs()
        {
            if (this.activeBuffs.Count == 0)
            {
                return;
            }

            foreach (BuffInstance inst in this.activeBuffs.Values)
            {
                this.DispatchRemove(inst);
            }
            this.activeBuffs.Clear();
            this.unit.eventDipatcher.DispatchEvent(UnitEvent.STATUS_CHANGE);
        }

        /// <summary>取最强制减速比例（0~1），无 Slow 时返回 0。</summary>
        public Fix64 GetSlowRate()
        {
            Fix64 maxSlow = Fix64.Zero;
            foreach (BuffInstance inst in this.activeBuffs.Values)
            {
                if (inst.category != BuffCategory.Slow)
                {
                    continue;
                }

                if (inst.param0 > maxSlow)
                {
                    maxSlow = inst.param0;
                }
            }
            return maxSlow;
        }

        public Fix64 GetSpeedMultiplier()
        {
            Fix64 slow = this.GetSlowRate();
            if (slow <= Fix64.Zero)
            {
                return Fix64.One;
            }
            return Fix64.One - slow;
        }

        public bool BlocksMovement()
        {
            foreach (BuffInstance inst in this.activeBuffs.Values)
            {
                if (inst.category == BuffCategory.Stun && inst.remainingTime > Fix64.Zero)
                {
                    return true;
                }
            }
            return false;
        }

        public override void OnTick(Fix64 deltaTime)
        {
            if (this.activeBuffs.Count == 0)
            {
                return;
            }

            Fix64 clock = this.unit.baseBattle.curClock;
            this.expiredBuffIds.Clear();

            foreach (KeyValuePair<int, BuffInstance> pair in this.activeBuffs)
            {
                BuffInstance inst = pair.Value;
                inst.remainingTime -= deltaTime;

                if (inst.category == BuffCategory.Dot && inst.tickInterval > Fix64.Zero && inst.tickDamage > 0)
                {
                    this.TickDot(inst, clock);
                }

                if (inst.remainingTime <= Fix64.Zero)
                {
                    this.expiredBuffIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < this.expiredBuffIds.Count; i++)
            {
                int buffId = this.expiredBuffIds[i];
                if (this.activeBuffs.TryGetValue(buffId, out BuffInstance inst))
                {
                    this.activeBuffs.Remove(buffId);
                    this.DispatchRemove(inst);
                }
            }
        }

        public override void Dispose()
        {
            this.ClearAllBuffs();
            base.Dispose();
        }

        private static Fix64 ComputeNextTickTime(Fix64 clock, BuffDef def)
        {
            if (def.category != BuffCategory.Dot || def.tickInterval <= Fix64.Zero)
            {
                return Fix64.Zero;
            }
            return clock + def.tickInterval;
        }

        private void TickDot(BuffInstance inst, Fix64 clock)
        {
            if (clock < inst.nextTickTime)
            {
                return;
            }

            BattleUnit_Monster monster = this.unit as BattleUnit_Monster;
            if (monster == null)
            {
                return;
            }

            BattleMonsterDamage.ApplyDamage(monster, inst.tickDamage);
            inst.nextTickTime = clock + inst.tickInterval;
        }

        private void DispatchRemove(BuffInstance inst)
        {
            if (this.unit == null)
            {
                return;
            }
            this.unit.eventDipatcher.DispatchEvent<BuffEventPayload>(
                UnitEvent.BUFF_REMOVE,
                BuffEventPayload.FromInstance(inst, false));
        }

        private void NotifyStatusChangeIfNeeded(BuffCategory category)
        {
            if (category == BuffCategory.Stun && this.unit != null)
            {
                this.unit.eventDipatcher.DispatchEvent(UnitEvent.STATUS_CHANGE);
            }
        }
    }
}
