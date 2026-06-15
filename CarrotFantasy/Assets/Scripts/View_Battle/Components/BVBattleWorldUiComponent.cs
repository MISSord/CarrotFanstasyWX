using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗内高频 UI：血条与伤害飘字分别使用独立 World Space Canvas，减轻单位节点下 Canvas 合并与层级开销。
    /// </summary>
    public class BVBattleWorldUiComponent : BaseBattleViewComponent
    {
        private const int HpCanvasSortOrder = 17;
        private const int DamageCanvasSortOrder = 18;
        private const float WorldCanvasWidth = 1.1059f;
        private const float WorldCanvasHeight = 0.1f;
        private const int DamageTextFontSize = 12;
        private static readonly Vector2 DamageTextSize = new Vector2(0.42f, 0.14f);
        private static readonly Vector3 DamageFloatLocalOffset = new Vector3(0f, 0.08f, 0f);
        private static readonly Vector3 DamageFloatVelocity = new Vector3(0f, 0.45f, 0f);

        private GameObject hpCanvasGo;
        private GameObject damageCanvasGo;
        private RectTransform hpCanvasRect;
        private RectTransform damageCanvasRect;

        private readonly Stack<Text> damageTextPool = new Stack<Text>();
        private readonly List<DamageFloatEntry> activeFloats = new List<DamageFloatEntry>();

        private struct DamageFloatEntry
        {
            public Text text;
            public RectTransform rect;
            public float remain;
            public Vector3 velocity;
        }

        public BVBattleWorldUiComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.WORLD_UI;
        }

        public override void Init()
        {
            this.EnsureCanvasesReady();
        }

        /// <summary>保证血条/飘字 Canvas 挂在 BattleRoot 下。</summary>
        public void EnsureCanvasesReady()
        {
            GameObject viewRoot = this.battleView != null ? this.battleView.rootGameObject : null;
            if (viewRoot == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] 战斗视图根节点无效，无法创建 World UI Canvas。");
                return;
            }

            Transform parent = viewRoot.transform;
            if (IsCanvasAttached(this.hpCanvasGo, parent) && IsCanvasAttached(this.damageCanvasGo, parent))
            {
                if (this.hpCanvasRect == null && this.hpCanvasGo != null)
                {
                    this.hpCanvasRect = this.hpCanvasGo.GetComponent<RectTransform>();
                }

                if (this.damageCanvasRect == null && this.damageCanvasGo != null)
                {
                    this.damageCanvasRect = this.damageCanvasGo.GetComponent<RectTransform>();
                }

                return;
            }

            DestroyCanvas(ref this.hpCanvasGo, ref this.hpCanvasRect);
            DestroyCanvas(ref this.damageCanvasGo, ref this.damageCanvasRect);

            this.hpCanvasGo = CreateWorldUiCanvas("BattleHpBarCanvas", HpCanvasSortOrder, parent);
            this.damageCanvasGo = CreateWorldUiCanvas("BattleDamageFloatCanvas", DamageCanvasSortOrder, parent);
            this.hpCanvasRect = this.hpCanvasGo.GetComponent<RectTransform>();
            this.damageCanvasRect = this.damageCanvasGo.GetComponent<RectTransform>();
        }

        static bool IsCanvasAttached(GameObject canvasGo, Transform expectedParent)
        {
            return canvasGo != null && expectedParent != null && canvasGo.transform.parent == expectedParent;
        }

        static void DestroyCanvas(ref GameObject canvasGo, ref RectTransform canvasRect)
        {
            if (canvasGo != null)
            {
                Object.Destroy(canvasGo);
            }

            canvasGo = null;
            canvasRect = null;
        }

        /// <summary>从 MonsterCanvas 预制体提取 HPSlider，挂到共享 BattleHpBarCanvas 下（不保留独立 Canvas）。</summary>
        public GameObject CreateMonsterHpBar(GameObject monsterCanvasTemplate)
        {
            this.EnsureCanvasesReady();

            if (monsterCanvasTemplate == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] MonsterCanvas 模板为空。");
                return null;
            }

            if (this.hpCanvasRect == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] BattleHpBarCanvas 未就绪。");
                return null;
            }

            GameObject canvasInstance = GameObject.Instantiate(monsterCanvasTemplate);
            Transform sliderTr = canvasInstance.transform.Find("HPSlider");
            if (sliderTr == null)
            {
                Object.Destroy(canvasInstance);
                Debug.LogError("[BVBattleWorldUiComponent] MonsterCanvas 预制体缺少 HPSlider。");
                return null;
            }

            RectTransform canvasRect = canvasInstance.GetComponent<RectTransform>();
            RectTransform sliderRect = sliderTr.GetComponent<RectTransform>();
            if (canvasRect != null && sliderRect != null)
            {
                sliderRect.sizeDelta = canvasRect.sizeDelta;
                sliderRect.pivot = canvasRect.pivot;
                sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
                sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
                sliderRect.anchoredPosition = Vector2.zero;
            }

            sliderTr.SetParent(this.hpCanvasRect, false);
            sliderTr.localScale = Vector3.one;
            Object.Destroy(canvasInstance);
            return sliderTr.gameObject;
        }

        private static GameObject CreateWorldUiCanvas(string name, int sortOrder, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(WorldCanvasWidth, WorldCanvasHeight);
            rect.anchoredPosition3D = Vector3.zero;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;

            GraphicRaycaster raycaster = go.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            return go;
        }

        public void AttachHpBarToSharedCanvas(RectTransform hpBarRoot)
        {
            if (hpBarRoot == null || this.hpCanvasRect == null) return;
            hpBarRoot.SetParent(this.hpCanvasRect, worldPositionStays: true);
        }

        public void DetachHpBarToUnit(RectTransform hpBarRoot, Transform unitRoot)
        {
            if (hpBarRoot == null || unitRoot == null) return;
            hpBarRoot.SetParent(unitRoot, worldPositionStays: true);
        }

        public void SyncHpBarWorldPosition(RectTransform hpBarRoot, Transform unitRoot, Vector3 localOffset)
        {
            if (hpBarRoot == null || unitRoot == null) return;
            hpBarRoot.position = unitRoot.TransformPoint(localOffset);
        }

        public void PlayDamageFloat(Vector3 worldPosition, int damage)
        {
            if (this.damageCanvasRect == null || damage <= 0) return;

            Text text = this.RentDamageText();
            RectTransform rect = text.rectTransform;
            rect.SetParent(this.damageCanvasRect, false);
            this.ApplyDamageTextLayout(text);
            text.text = damage.ToString();
            text.enabled = true;
            rect.localPosition = this.damageCanvasRect.InverseTransformPoint(worldPosition) + DamageFloatLocalOffset;
            text.color = new Color(1f, 0.35f, 0.2f, 1f);

            this.activeFloats.Add(new DamageFloatEntry
            {
                text = text,
                rect = rect,
                remain = 0.75f,
                velocity = DamageFloatVelocity,
            });
        }

        private void ApplyDamageTextLayout(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = DamageTextFontSize;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            if (text.font == null)
            {
                text.font = GetDefaultUIFont();
            }

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = DamageTextSize;
            rt.localScale = Vector3.one;
        }

        private Text RentDamageText()
        {
            Text text;
            if (this.damageTextPool.Count > 0)
            {
                text = this.damageTextPool.Pop();
            }
            else
            {
                GameObject go = new GameObject("DamageFloatText");
                text = go.AddComponent<Text>();
                this.ApplyDamageTextLayout(text);
            }

            return text;
        }

        static Font cachedDefaultUiFont;

        private static Font GetDefaultUIFont()
        {
            if (cachedDefaultUiFont != null)
            {
                return cachedDefaultUiFont;
            }

            Font f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (f == null)
            {
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            cachedDefaultUiFont = f;
            return cachedDefaultUiFont;
        }

        private void ReturnDamageText(Text text)
        {
            if (text == null) return;
            text.enabled = false;
            text.transform.SetParent(this.damageCanvasRect, false);
            this.damageTextPool.Push(text);
        }

        public override void OnTick(float time)
        {
            if (this.activeFloats.Count == 0) return;
            for (int i = this.activeFloats.Count - 1; i >= 0; i--)
            {
                DamageFloatEntry e = this.activeFloats[i];
                e.remain -= time;
                e.rect.localPosition += e.velocity * time;
                if (e.text == null)
                {
                    this.activeFloats.RemoveAt(i);
                    continue;
                }

                if (e.remain <= 0f)
                {
                    this.ReturnDamageText(e.text);
                    this.activeFloats.RemoveAt(i);
                }
                else
                {
                    Color c = e.text.color;
                    c.a = Mathf.Clamp01(e.remain / 0.75f);
                    e.text.color = c;
                    this.activeFloats[i] = e;
                }
            }
        }

        public override void ClearGameInfo()
        {
            for (int i = this.activeFloats.Count - 1; i >= 0; i--)
            {
                this.ReturnDamageText(this.activeFloats[i].text);
            }

            this.activeFloats.Clear();
            base.ClearGameInfo();
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            if (this.hpCanvasGo != null)
            {
                UnityEngine.Object.Destroy(this.hpCanvasGo);
                this.hpCanvasGo = null;
                this.hpCanvasRect = null;
            }

            if (this.damageCanvasGo != null)
            {
                UnityEngine.Object.Destroy(this.damageCanvasGo);
                this.damageCanvasGo = null;
                this.damageCanvasRect = null;
            }

            this.damageTextPool.Clear();
            base.Dispose();
        }
    }
}
