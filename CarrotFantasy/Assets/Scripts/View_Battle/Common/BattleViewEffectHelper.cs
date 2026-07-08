using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗视图层通用特效（建造/死亡爆炸等），统一对象池与预加载模板。</summary>
    public static class BattleViewEffectHelper
    {
        private const float DestroyEffectDuration = 0.5f;
        private const float BuildEffectDuration = 0.5f;

        static readonly HashSet<GameObject> activeBuildEffects = new HashSet<GameObject>();

        public static void EnsureDestroyEffectPoolRegistered()
        {
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.DestroyEffect);
        }

        public static void EnsureBuildEffectPoolRegistered()
        {
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.BuildEffect);
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
                ReturnDestroyEffect(captured);
            }, DestroyEffectDuration);
        }

        public static void PlayBuildAt(Vector3 worldPosition)
        {
            EnsureBuildEffectPoolRegistered();

            GameObject effect = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.BuildEffect);
            if (effect == null)
            {
                GameObject template;
                if (!BattleViewPrefabPreloader.TryGetTemplate(
                    FightViewPrefabAb.FightPartBundle,
                    FightViewPrefabAb.BuildEffect,
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

            activeBuildEffects.Add(effect);
            GameObject captured = effect;
            Sche.DelayExeOnceTimes(() =>
            {
                ReturnBuildEffect(captured);
            }, BuildEffectDuration);
        }

        public static void PlayBuildAt(BattleUnit unit)
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

            PlayBuildAt(new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0f));
        }

        public static void ClearActiveBuildEffects()
        {
            if (activeBuildEffects.Count == 0)
            {
                return;
            }

            GameObject[] snapshot = new GameObject[activeBuildEffects.Count];
            activeBuildEffects.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
            {
                ReturnBuildEffect(snapshot[i]);
            }
        }

        static void ReturnDestroyEffect(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }

            Animator anim = effect.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
            }

            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, effect);
        }

        static void ReturnBuildEffect(GameObject effect)
        {
            if (effect == null || !activeBuildEffects.Remove(effect))
            {
                return;
            }

            Animator anim = effect.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
            }

            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.BuildEffect, effect);
        }

        public static void ResetTemplates()
        {
            activeBuildEffects.Clear();
        }
    }
}
