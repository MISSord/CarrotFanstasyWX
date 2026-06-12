using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>怪物头顶 Buff 图标条（挂在 MonsterCanvas / 共享血条 Canvas 下）。</summary>
    public class MonsterBuffIconBar
    {
        private const float IconSize = 22f;
        private const float IconSpacing = 4f;
        private static readonly Vector2 BarLocalOffset = new Vector2(0f, -28f);

        private readonly Dictionary<int, BuffIconSlot> slots = new Dictionary<int, BuffIconSlot>();
        private RectTransform barRoot;
        private Font uiFont;

        private class BuffIconSlot
        {
            public int buffId;
            public BuffCategory category;
            public RectTransform root;
            public Text timerText;
            public float remainingSeconds;
        }

        public bool IsCreated
        {
            get { return this.barRoot != null; }
        }

        public void Create(RectTransform hpCanvasRoot)
        {
            this.ClearAll();
            if (hpCanvasRoot == null)
            {
                return;
            }

            GameObject rootGo = new GameObject("BuffIconBar");
            this.barRoot = rootGo.AddComponent<RectTransform>();
            this.barRoot.SetParent(hpCanvasRoot, false);
            this.barRoot.anchorMin = new Vector2(0.5f, 0.5f);
            this.barRoot.anchorMax = new Vector2(0.5f, 0.5f);
            this.barRoot.pivot = new Vector2(0.5f, 0.5f);
            this.barRoot.anchoredPosition = BarLocalOffset;
            this.barRoot.sizeDelta = new Vector2(120f, IconSize);
            this.barRoot.localScale = Vector3.one;
            this.uiFont = GetDefaultFont();
        }

        public void ApplyOrRefresh(BuffEventPayload payload)
        {
            if (this.barRoot == null || payload == null)
            {
                return;
            }

            float remain = (float)payload.remainingTime;
            if (this.slots.TryGetValue(payload.buffId, out BuffIconSlot slot))
            {
                slot.remainingSeconds = remain;
                this.UpdateTimerText(slot);
                this.PulseIcon(slot.root);
                return;
            }

            if (!BuffViewVisualCatalog.TryGetStyle(payload.buffId, out BuffViewVisualCatalog.BuffVisualStyle style))
            {
                BuffViewVisualCatalog.TryGetStyle(payload.category, out style);
            }

            slot = this.CreateSlot(payload.buffId, payload.category, style, remain);
            this.slots.Add(payload.buffId, slot);
            this.RelayoutIcons();
        }

        public void Remove(int buffId)
        {
            if (!this.slots.TryGetValue(buffId, out BuffIconSlot slot))
            {
                return;
            }

            if (slot.root != null)
            {
                Object.Destroy(slot.root.gameObject);
            }

            this.slots.Remove(buffId);
            this.RelayoutIcons();
        }

        public void ClearAll()
        {
            foreach (KeyValuePair<int, BuffIconSlot> pair in this.slots)
            {
                if (pair.Value.root != null)
                {
                    Object.Destroy(pair.Value.root.gameObject);
                }
            }

            this.slots.Clear();
            if (this.barRoot != null)
            {
                Object.Destroy(this.barRoot.gameObject);
                this.barRoot = null;
            }
        }

        public void OnTick(float deltaTime)
        {
            if (this.slots.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, BuffIconSlot> pair in this.slots)
            {
                BuffIconSlot slot = pair.Value;
                slot.remainingSeconds -= deltaTime;
                if (slot.remainingSeconds < 0f)
                {
                    slot.remainingSeconds = 0f;
                }

                this.UpdateTimerText(slot);
            }
        }

        public bool HasCategory(BuffCategory category)
        {
            foreach (KeyValuePair<int, BuffIconSlot> pair in this.slots)
            {
                if (pair.Value.category == category)
                {
                    return true;
                }
            }

            return false;
        }

        private BuffIconSlot CreateSlot(int buffId, BuffCategory category, BuffViewVisualCatalog.BuffVisualStyle style, float remain)
        {
            GameObject go = new GameObject("Buff_" + buffId);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(this.barRoot, false);
            rt.sizeDelta = new Vector2(IconSize, IconSize);

            Image bg = go.AddComponent<Image>();
            bg.sprite = GetWhiteSprite();
            bg.color = style.iconColor;
            bg.raycastTarget = false;

            GameObject labelGo = new GameObject("Label");
            RectTransform labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            Text label = labelGo.AddComponent<Text>();
            label.text = style.label;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 14;
            label.color = Color.white;
            label.font = this.uiFont;
            label.raycastTarget = false;

            GameObject timerGo = new GameObject("Timer");
            RectTransform timerRt = timerGo.AddComponent<RectTransform>();
            timerRt.SetParent(rt, false);
            timerRt.anchorMin = new Vector2(0.5f, 0f);
            timerRt.anchorMax = new Vector2(0.5f, 0f);
            timerRt.pivot = new Vector2(0.5f, 1f);
            timerRt.anchoredPosition = new Vector2(0f, -2f);
            timerRt.sizeDelta = new Vector2(IconSize + 6f, 12f);

            Text timer = timerGo.AddComponent<Text>();
            timer.alignment = TextAnchor.MiddleCenter;
            timer.fontSize = 10;
            timer.color = new Color(1f, 1f, 1f, 0.95f);
            timer.font = this.uiFont;
            timer.raycastTarget = false;

            BuffIconSlot slot = new BuffIconSlot
            {
                buffId = buffId,
                category = category,
                root = rt,
                timerText = timer,
                remainingSeconds = remain,
            };
            this.UpdateTimerText(slot);
            return slot;
        }

        private void UpdateTimerText(BuffIconSlot slot)
        {
            if (slot.timerText == null)
            {
                return;
            }

            int sec = Mathf.CeilToInt(slot.remainingSeconds);
            slot.timerText.text = sec > 0 ? sec.ToString() : "";
        }

        private void RelayoutIcons()
        {
            int index = 0;
            int count = this.slots.Count;
            float totalWidth = count * IconSize + (count > 0 ? (count - 1) * IconSpacing : 0f);
            float startX = -totalWidth * 0.5f + IconSize * 0.5f;

            foreach (KeyValuePair<int, BuffIconSlot> pair in this.slots)
            {
                RectTransform rt = pair.Value.root;
                if (rt == null)
                {
                    continue;
                }

                rt.anchoredPosition = new Vector2(startX + index * (IconSize + IconSpacing), 0f);
                index++;
            }
        }

        private void PulseIcon(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            rt.localScale = new Vector3(1.15f, 1.15f, 1f);
        }

        public void ResetPulsedIconScales()
        {
            foreach (KeyValuePair<int, BuffIconSlot> pair in this.slots)
            {
                RectTransform rt = pair.Value.root;
                if (rt != null && rt.localScale != Vector3.one)
                {
                    rt.localScale = Vector3.one;
                }
            }
        }

        private static Sprite whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return whiteSprite;
        }

        static Font cachedDefaultFont;

        private static Font GetDefaultFont()
        {
            if (cachedDefaultFont != null)
            {
                return cachedDefaultFont;
            }

            Font f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (f == null)
            {
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            cachedDefaultFont = f;
            return cachedDefaultFont;
        }
    }
}
