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
            Transform parent = this.battleView.rootGameObject.transform;

            this.hpCanvasGo = CreateWorldUiCanvas("BattleHpBarCanvas", HpCanvasSortOrder, parent);
            this.damageCanvasGo = CreateWorldUiCanvas("BattleDamageFloatCanvas", DamageCanvasSortOrder, parent);
            this.hpCanvasRect = this.hpCanvasGo.GetComponent<RectTransform>();
            this.damageCanvasRect = this.damageCanvasGo.GetComponent<RectTransform>();
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
            rect.sizeDelta = new Vector2(1f, 1f);
            rect.anchoredPosition3D = Vector3.zero;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

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
            rect.localScale = Vector3.one;
            text.text = damage.ToString();
            text.enabled = true;
            rect.position = worldPosition + new Vector3(0f, 0.15f, 0f);
            text.color = new Color(1f, 0.35f, 0.2f, 1f);

            this.activeFloats.Add(new DamageFloatEntry
            {
                text = text,
                rect = rect,
                remain = 0.75f,
                velocity = new Vector3(0f, 0.85f, 0f),
            });
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
                text.alignment = TextAnchor.MiddleCenter;
                text.fontSize = 22;
                text.color = new Color(1f, 0.35f, 0.2f, 1f);
                text.font = GetDefaultUIFont();
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;

                RectTransform rt = text.rectTransform;
                rt.sizeDelta = new Vector2(120f, 40f);
            }

            return text;
        }

        private static Font GetDefaultUIFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null)
            {
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return f;
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
