using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 按钮按下时用 DOTween 缩放到 <see cref="pressedScale"/>，抬起或指针离开交互区域时恢复到 <see cref="releasedScale"/>。
    /// 挂在与 <see cref="Button"/> 同一物体上；需要 Graphic 可射线检测（如 Image）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonScalePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("缩放目标，默认同节点 RectTransform")]
        [SerializeField]
        private RectTransform scaleTarget;

        [Tooltip("抬起 / 常态本地缩放")]
        [SerializeField]
        private Vector3 releasedScale = Vector3.one;

        [Tooltip("按下时本地缩放（相对设计比例）")]
        [SerializeField]
        private Vector3 pressedScale = new Vector3(0.92f, 0.92f, 1f);

        [SerializeField]
        private float pressDuration = 0.08f;

        [SerializeField]
        private float releaseDuration = 0.12f;

        [SerializeField]
        private Ease pressEase = Ease.OutQuad;

        [SerializeField]
        private Ease releaseEase = Ease.OutQuad;

        [Tooltip("使用 Unscaled DeltaTime（暂停时仍可播放）")]
        [SerializeField]
        private bool useUnscaledTime;

        [Tooltip("启用时用 releasedScale 重置一次，避免外部改过 Scale 不对齐")]
        [SerializeField]
        private bool resetToReleasedOnEnable = true;

        private Button button;
        private bool isPressed;

        private void Awake()
        {
            this.button = GetComponent<Button>();
            if (this.scaleTarget == null)
            {
                this.scaleTarget = transform as RectTransform;
                if (this.scaleTarget == null)
                {
                    this.scaleTarget = GetComponent<RectTransform>();
                }
            }
        }

        private void OnEnable()
        {
            this.isPressed = false;
            if (this.resetToReleasedOnEnable && this.scaleTarget != null)
            {
                this.scaleTarget.DOKill(false);
                this.scaleTarget.localScale = this.releasedScale;
            }
        }

        private void OnDisable()
        {
            if (this.scaleTarget != null)
            {
                this.scaleTarget.DOKill(false);
                this.scaleTarget.localScale = this.releasedScale;
            }

            this.isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (this.button != null && !this.button.interactable)
            {
                return;
            }

            this.isPressed = true;
            this.TweenToScale(this.pressedScale, this.pressDuration, this.pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            this.ApplyRelease();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (this.isPressed)
            {
                this.ApplyRelease();
            }
        }

        private void ApplyRelease()
        {
            if (!this.isPressed)
            {
                return;
            }

            this.isPressed = false;
            this.TweenToScale(this.releasedScale, this.releaseDuration, this.releaseEase);
        }

        private void TweenToScale(Vector3 target, float duration, Ease ease)
        {
            if (this.scaleTarget == null)
            {
                return;
            }

            this.scaleTarget.DOKill(false);
            Tween tw = this.scaleTarget.DOScale(target, Mathf.Max(0f, duration)).SetEase(ease);
            if (this.useUnscaledTime)
            {
                tw.SetUpdate(true);
            }
        }
    }
}
