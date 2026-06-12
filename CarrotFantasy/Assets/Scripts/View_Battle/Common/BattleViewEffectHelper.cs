using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗视图层通用特效（死亡爆炸等），统一对象池与预加载模板。</summary>
    public static class BattleViewEffectHelper
    {
        private const float DestroyEffectDuration = 0.5f;

        public static void EnsureDestroyEffectPoolRegistered()
        {
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.DestroyEffect);
        }

        public static void PlayDestroyAt(BattleUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            if (tran == null)
            {
                return;
            }

            PlayDestroyAt(new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0f));
        }

        public static void PlayDestroyAt(Vector3 worldPosition)
        {
            EnsureDestroyEffectPoolRegistered();

            GameObject effect = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DestroyEffect);
            if (effect == null)
            {
                GameObject template;
                if (!BattleViewPrefabPreloader.TryGetTemplate(
                    FightViewPrefabAb.FightPartBundle,
                    FightViewPrefabAb.DestoryEffect,
                    out template))
                {
                    return;
                }

                effect = GameObject.Instantiate(template);
            }

            effect.transform.position = worldPosition;
            Animator animator = effect.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
            }

            GameObject captured = effect;
            Sche.DelayExeOnceTimes(() =>
            {
                if (captured == null)
                {
                    return;
                }

                Animator anim = captured.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.enabled = false;
                }

                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, captured);
            }, DestroyEffectDuration);
        }

        public static void ResetTemplates()
        {
        }
    }
}
