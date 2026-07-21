using System.Collections.Generic;

namespace CarrotFantasy
{
    /// <summary>将 Effect / Item 编译为开战参数。</summary>
    public static class RoguelikeEffectCompiler
    {
        public static void CompileEffectIds(IList<int> effectIds, RoguelikeBattleModifiers mods)
        {
            if (mods == null || effectIds == null)
            {
                return;
            }

            for (int i = 0; i < effectIds.Count; i++)
            {
                ApplyEffectId(effectIds[i], mods);
            }
        }

        public static void CompileItemIds(IList<int> itemIds, RoguelikeBattleModifiers mods)
        {
            if (mods == null || itemIds == null)
            {
                return;
            }

            for (int i = 0; i < itemIds.Count; i++)
            {
                if (!RoguelikeItemConfigReader.Instance.TryGet(itemIds[i], out RoguelikeItemDef item))
                {
                    continue;
                }

                if (item.effectIds == null)
                {
                    continue;
                }

                for (int e = 0; e < item.effectIds.Length; e++)
                {
                    ApplyEffectId(item.effectIds[e], mods);
                }
            }
        }

        public static int SumStartingRoguelikeGold(IList<int> effectIds)
        {
            int sum = 0;
            if (effectIds == null)
            {
                return sum;
            }

            for (int i = 0; i < effectIds.Count; i++)
            {
                if (!RoguelikeEffectConfigReader.Instance.TryGet(effectIds[i], out RoguelikeEffectDef def))
                {
                    continue;
                }

                if (def.type == RoguelikeEffectType.StartingRoguelikeGold && def.param0 > 0)
                {
                    sum += def.param0;
                }
            }

            return sum;
        }

        static void ApplyEffectId(int effectId, RoguelikeBattleModifiers mods)
        {
            if (effectId <= 0 ||
                !RoguelikeEffectConfigReader.Instance.TryGet(effectId, out RoguelikeEffectDef def))
            {
                return;
            }

            switch (def.type)
            {
                case RoguelikeEffectType.StartCoin:
                    mods.StartCoinBonus += def.param0;
                    break;
                case RoguelikeEffectType.TowerDamagePercent:
                    mods.TowerDamagePercentBonus += def.param0;
                    break;
                case RoguelikeEffectType.GrantGlobalBuff:
                    if (def.param0 > 0 && !mods.GlobalBuffIds.Contains(def.param0))
                    {
                        mods.GlobalBuffIds.Add(def.param0);
                    }
                    break;
                case RoguelikeEffectType.StartingRoguelikeGold:
                    // 仅进图时结算，开战不处理。
                    break;
            }
        }
    }
}
