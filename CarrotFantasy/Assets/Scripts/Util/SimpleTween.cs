using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public enum SimpleEase
    {
        Linear,
        OutQuad,
        OutQuint,
    }

    public enum SimpleLoopType
    {
        Restart,
        Yoyo,
    }

    /// <summary>
    /// 轻量 Tween，替代 DOTween.dll（避免 HybridCLR Player 编译 CS0012）。
    /// </summary>
    public sealed class SimpleTween
    {
        private static readonly List<SimpleTween> Active = new List<SimpleTween>();
        private static TweenRunner runner;

        private Transform target;
        private Component killToken;
        private Func<Vector3> getVector3;
        private Action<Vector3> setVector3;
        private Func<float> getFloat;
        private Action<float> setFloat;
        private Func<Color> getColor;
        private Action<Color> setColor;
        private Vector3 startVector3;
        private Vector3 endVector3;
        private float startFloat;
        private float endFloat;
        private Color startColor;
        private Color endColor;
        private float duration;
        private float elapsed;
        private bool useVector3;
        private bool useColor;
        private bool paused = true;
        private bool killed;
        private bool autoKill = true;
        private bool useUnscaledTime;
        private int loops; // -1 = infinite
        private SimpleLoopType loopType;
        private bool yoyoForward = true;
        private SimpleEase ease = SimpleEase.Linear;
        private Action onComplete;
        private Coroutine routine;

        public static SimpleTween LocalMoveX(Transform transform, float endX, float duration)
        {
            Vector3 end = transform.localPosition;
            end.x = endX;
            return ToVector3(
                transform,
                () => transform.localPosition,
                v => transform.localPosition = v,
                end,
                duration);
        }

        public static SimpleTween LocalMoveY(Transform transform, float endY, float duration)
        {
            Vector3 end = transform.localPosition;
            end.y = endY;
            return ToVector3(
                transform,
                () => transform.localPosition,
                v => transform.localPosition = v,
                end,
                duration);
        }

        public static SimpleTween Scale(Transform transform, Vector3 endScale, float duration, SimpleEase ease)
        {
            SimpleTween tw = ToVector3(
                transform,
                () => transform.localScale,
                v => transform.localScale = v,
                endScale,
                duration);
            tw.ease = ease;
            return tw;
        }

        public static SimpleTween To(
            Func<Vector3> getter,
            Action<Vector3> setter,
            Vector3 endValue,
            float duration,
            SimpleEase ease = SimpleEase.Linear)
        {
            SimpleTween tw = ToVector3(null, getter, setter, endValue, duration);
            tw.ease = ease;
            tw.paused = false;
            return tw;
        }

        public static SimpleTween To(
            Func<float> getter,
            Action<float> setter,
            float endValue,
            float duration,
            SimpleEase ease = SimpleEase.Linear)
        {
            EnsureRunner();
            SimpleTween tw = new SimpleTween
            {
                getFloat = getter,
                setFloat = setter,
                startFloat = getter(),
                endFloat = endValue,
                duration = Mathf.Max(0.0001f, duration),
                useVector3 = false,
                useColor = false,
                paused = false,
                ease = ease,
            };
            tw.StartRoutine();
            return tw;
        }

        public static SimpleTween To(
            Func<Color> getter,
            Action<Color> setter,
            Color endValue,
            float duration,
            SimpleEase ease = SimpleEase.Linear)
        {
            EnsureRunner();
            SimpleTween tw = new SimpleTween
            {
                getColor = getter,
                setColor = setter,
                startColor = getter(),
                endColor = endValue,
                duration = Mathf.Max(0.0001f, duration),
                useVector3 = false,
                useColor = true,
                paused = false,
                ease = ease,
            };
            tw.StartRoutine();
            return tw;
        }

        public static void Kill(Transform transform, bool complete = false)
        {
            KillByTarget(transform, complete);
        }

        public static void Kill(Component component, bool complete = false)
        {
            if (component == null)
            {
                return;
            }

            KillByTarget(component.transform, complete);
            KillByToken(component, complete);
        }

        public SimpleTween SetAutoKill(bool value)
        {
            this.autoKill = value;
            return this;
        }

        public SimpleTween Pause()
        {
            this.paused = true;
            return this;
        }

        public SimpleTween Play()
        {
            if (this.killed)
            {
                return this;
            }

            this.paused = false;
            if (this.routine == null)
            {
                this.CaptureStart();
                this.elapsed = 0f;
                this.StartRoutine();
            }

            return this;
        }

        public SimpleTween Restart()
        {
            this.killed = false;
            this.elapsed = 0f;
            this.yoyoForward = true;
            this.CaptureStart();
            this.paused = false;
            if (this.routine == null)
            {
                this.StartRoutine();
            }

            return this;
        }

        public SimpleTween SetLoops(int loopCount, SimpleLoopType type)
        {
            this.loops = loopCount;
            this.loopType = type;
            return this;
        }

        public SimpleTween SetEase(SimpleEase value)
        {
            this.ease = value;
            return this;
        }

        public SimpleTween SetUpdate(bool unscaled)
        {
            this.useUnscaledTime = unscaled;
            return this;
        }

        public SimpleTween SetTarget(Component token)
        {
            this.killToken = token;
            if (token != null)
            {
                this.target = token.transform;
            }

            return this;
        }

        public SimpleTween OnComplete(Action callback)
        {
            this.onComplete = callback;
            return this;
        }

        private static SimpleTween ToVector3(
            Transform transform,
            Func<Vector3> getter,
            Action<Vector3> setter,
            Vector3 endValue,
            float duration)
        {
            EnsureRunner();
            SimpleTween tw = new SimpleTween
            {
                target = transform,
                getVector3 = getter,
                setVector3 = setter,
                endVector3 = endValue,
                duration = Mathf.Max(0.0001f, duration),
                useVector3 = true,
                useColor = false,
                paused = true,
            };
            tw.CaptureStart();
            tw.StartRoutine();
            return tw;
        }

        private void CaptureStart()
        {
            if (this.useVector3)
            {
                this.startVector3 = this.getVector3();
            }
            else if (this.useColor)
            {
                this.startColor = this.getColor();
            }
            else
            {
                this.startFloat = this.getFloat();
            }
        }

        private void StartRoutine()
        {
            EnsureRunner();
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            if (this.routine != null)
            {
                runner.StopCoroutine(this.routine);
            }

            this.routine = runner.StartCoroutine(this.Tick());
        }

        private IEnumerator Tick()
        {
            while (!this.killed)
            {
                if (this.paused)
                {
                    yield return null;
                    continue;
                }

                if (this.IsTargetDestroyed())
                {
                    this.InternalKill(false);
                    yield break;
                }

                float dt = this.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                this.elapsed += dt;
                float t = Mathf.Clamp01(this.elapsed / this.duration);
                float eased = Evaluate(this.ease, t);

                if (this.useVector3)
                {
                    Vector3 from = this.yoyoForward ? this.startVector3 : this.endVector3;
                    Vector3 to = this.yoyoForward ? this.endVector3 : this.startVector3;
                    if (!this.TrySetVector3(Vector3.LerpUnclamped(from, to, eased)))
                    {
                        this.InternalKill(false);
                        yield break;
                    }
                }
                else if (this.useColor)
                {
                    Color from = this.yoyoForward ? this.startColor : this.endColor;
                    Color to = this.yoyoForward ? this.endColor : this.startColor;
                    if (!this.TrySetColor(Color.LerpUnclamped(from, to, eased)))
                    {
                        this.InternalKill(false);
                        yield break;
                    }
                }
                else
                {
                    float from = this.yoyoForward ? this.startFloat : this.endFloat;
                    float to = this.yoyoForward ? this.endFloat : this.startFloat;
                    if (!this.TrySetFloat(Mathf.LerpUnclamped(from, to, eased)))
                    {
                        this.InternalKill(false);
                        yield break;
                    }
                }

                if (t < 1f)
                {
                    yield return null;
                    continue;
                }

                bool continueLoop = false;
                if (this.loops < 0)
                {
                    continueLoop = true;
                }
                else if (this.loops > 1)
                {
                    this.loops--;
                    continueLoop = true;
                }

                if (continueLoop)
                {
                    this.elapsed = 0f;
                    if (this.loopType == SimpleLoopType.Yoyo)
                    {
                        this.yoyoForward = !this.yoyoForward;
                    }
                    else
                    {
                        if (this.useVector3)
                        {
                            if (!this.TrySetVector3(this.startVector3))
                            {
                                this.InternalKill(false);
                                yield break;
                            }
                        }
                        else if (this.useColor)
                        {
                            if (!this.TrySetColor(this.startColor))
                            {
                                this.InternalKill(false);
                                yield break;
                            }
                        }
                        else if (!this.TrySetFloat(this.startFloat))
                        {
                            this.InternalKill(false);
                            yield break;
                        }
                    }

                    yield return null;
                    continue;
                }

                this.onComplete?.Invoke();
                this.routine = null;
                if (this.autoKill)
                {
                    this.InternalKill(false);
                }
                else
                {
                    this.paused = true;
                }

                yield break;
            }
        }

        /// <summary>Unity 已销毁对象对 == null 为 true，但引用尚未清空。</summary>
        private bool IsTargetDestroyed()
        {
            if (!ReferenceEquals(this.target, null) && this.target == null)
            {
                return true;
            }

            if (!ReferenceEquals(this.killToken, null) && this.killToken == null)
            {
                return true;
            }

            return false;
        }

        private bool TrySetVector3(Vector3 value)
        {
            if (this.IsTargetDestroyed() || this.setVector3 == null)
            {
                return false;
            }

            try
            {
                this.setVector3(value);
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private bool TrySetColor(Color value)
        {
            if (this.IsTargetDestroyed() || this.setColor == null)
            {
                return false;
            }

            try
            {
                this.setColor(value);
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private bool TrySetFloat(float value)
        {
            if (this.IsTargetDestroyed() || this.setFloat == null)
            {
                return false;
            }

            try
            {
                this.setFloat(value);
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private void InternalKill(bool complete)
        {
            if (this.killed)
            {
                return;
            }

            this.killed = true;
            this.paused = true;
            if (complete && !this.IsTargetDestroyed())
            {
                if (this.useVector3)
                {
                    this.TrySetVector3(this.endVector3);
                }
                else if (this.useColor)
                {
                    this.TrySetColor(this.endColor);
                }
                else
                {
                    this.TrySetFloat(this.endFloat);
                }
            }

            if (this.routine != null && runner != null)
            {
                runner.StopCoroutine(this.routine);
                this.routine = null;
            }

            Active.Remove(this);
        }

        private static void KillByTarget(Transform transform, bool complete)
        {
            if (transform == null)
            {
                return;
            }

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                SimpleTween tw = Active[i];
                if (tw.target == transform)
                {
                    tw.InternalKill(complete);
                }
            }
        }

        private static void KillByToken(Component token, bool complete)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                SimpleTween tw = Active[i];
                if (tw.killToken == token)
                {
                    tw.InternalKill(complete);
                }
            }
        }

        private static float Evaluate(SimpleEase ease, float t)
        {
            switch (ease)
            {
                case SimpleEase.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case SimpleEase.OutQuint:
                    float u = 1f - t;
                    return 1f - u * u * u * u * u;
                default:
                    return t;
            }
        }

        private static void EnsureRunner()
        {
            if (runner != null)
            {
                return;
            }

            GameObject go = new GameObject("SimpleTweenRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<TweenRunner>();
        }

        private sealed class TweenRunner : MonoBehaviour
        {
        }
    }
}
