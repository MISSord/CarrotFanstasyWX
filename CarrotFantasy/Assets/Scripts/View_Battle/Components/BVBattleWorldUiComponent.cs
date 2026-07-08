using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗内高频 UI：血条与伤害飘字各用独立 Canvas，挂在全局 UILayer 下，
    /// 以 Screen Space Camera + UICamera 渲染，世界坐标每帧换算为 Canvas 局部坐标。
    /// </summary>
    public class BVBattleWorldUiComponent : BaseBattleViewComponent
    {
        private const int HpCanvasSortOrder = 17;
        private const int DamageCanvasSortOrder = 18;
        private const float CanvasPlaneDistance = 10f;
        private const float DamageFloatDuration = 0.75f;
        private static readonly Vector2 DamageFloatLocalOffset = new Vector2(0f, 8f);
        private static readonly Vector2 DamageFloatVelocity = new Vector2(0f, 45f);
        private static readonly Color DamageFloatBaseColor = new Color(1f, 0.35f, 0.2f, 1f);

        private GameObject hpCanvasGo;
        private GameObject damageCanvasGo;
        private RectTransform hpCanvasRect;
        private RectTransform damageCanvasRect;
        private GameObject hpSliderTemplate;
        private GameObject damageFloatTemplate;

        private readonly List<DamageFloatEntry> activeFloats = new List<DamageFloatEntry>();

        private struct DamageFloatEntry
        {
            public GameObject root;
            public Text text;
            public RectTransform rect;
            public float remain;
            public Vector2 velocity;
        }

        public BVBattleWorldUiComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.WORLD_UI;
        }

        public override void Init()
        {
            this.EnsureCanvasesReady();
            this.EnsureWorldUiPoolsRegistered();
            this.IsBuilt = true;
        }

        public override void ResetRound(BattleViewResetPass pass)
        {
            if (pass == BattleViewResetPass.BeforeModel)
            {
                this.ClearTransientEffects();
            }
        }

        void EnsureWorldUiPoolsRegistered()
        {
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.HpSlider);
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.DamageFloatText);

            if (this.hpSliderTemplate == null)
            {
                BattleViewPrefabPreloader.TryGetTemplate(
                    FightViewPrefabAb.FightPartBundle,
                    FightViewPrefabAb.HpSlider,
                    out this.hpSliderTemplate);
            }

            if (this.damageFloatTemplate == null)
            {
                BattleViewPrefabPreloader.TryGetTemplate(
                    FightViewPrefabAb.FightPartBundle,
                    FightViewPrefabAb.DamageFloatText,
                    out this.damageFloatTemplate);
            }
        }

        void ClearTransientEffects()
        {
            for (int i = this.activeFloats.Count - 1; i >= 0; i--)
            {
                this.ReturnDamageFloat(this.activeFloats[i].root);
            }

            this.activeFloats.Clear();
        }

        public override void ClearGameInfo()
        {
            this.ClearTransientEffects();
            this.hpSliderTemplate = null;
            this.damageFloatTemplate = null;
            this.DestroyWorldUiCanvases();
            base.ClearGameInfo();
            this.IsBuilt = false;
        }

        void DestroyWorldUiCanvases()
        {
            if (this.hpCanvasGo != null)
            {
                Object.Destroy(this.hpCanvasGo);
                this.hpCanvasGo = null;
                this.hpCanvasRect = null;
            }

            if (this.damageCanvasGo != null)
            {
                Object.Destroy(this.damageCanvasGo);
                this.damageCanvasGo = null;
                this.damageCanvasRect = null;
            }
        }

        /// <summary>保证血条/飘字 Canvas 挂在 UILayer 下并由 UICamera 拍摄。</summary>
        public void EnsureCanvasesReady()
        {
            if (ViewManager.Instance == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] ViewManager 未初始化，无法创建战斗 UI Canvas。");
                return;
            }

            GameObject uiRoot = ViewManager.Instance.GetUIRoot();
            Camera uiCamera = ViewManager.Instance.GetUICamera();
            if (uiRoot == null || uiCamera == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] UILayer 或 UICamera 未就绪。");
                return;
            }

            Transform parent = uiRoot.transform;
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

            this.hpCanvasGo = CreateBattleUiCanvas("BattleHpBarCanvas", HpCanvasSortOrder, parent, uiCamera);
            this.damageCanvasGo = CreateBattleUiCanvas("BattleDamageFloatCanvas", DamageCanvasSortOrder, parent, uiCamera);
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

        /// <summary>从对象池取出 HPSlider 并挂到共享 BattleHpBarCanvas 下。</summary>
        public GameObject CreateHpBar()
        {
            this.EnsureCanvasesReady();
            this.EnsureWorldUiPoolsRegistered();

            if (this.hpCanvasRect == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] BattleHpBarCanvas 未就绪。");
                return null;
            }

            GameObject hpBarGo = this.RentHpBar();
            if (hpBarGo == null)
            {
                return null;
            }

            Slider slider = hpBarGo.GetComponent<Slider>();
            if (slider != null)
            {
                slider.value = 1f;
            }

            hpBarGo.transform.localEulerAngles = Vector3.zero;
            return hpBarGo;
        }

        /// <summary>血条用毕回收到对象池。</summary>
        public void ReturnHpBar(GameObject hpBarGo)
        {
            if (hpBarGo == null)
            {
                return;
            }

            Slider slider = hpBarGo.GetComponent<Slider>();
            if (slider != null)
            {
                slider.value = 1f;
            }

            hpBarGo.SetActive(false);
            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.HpSlider, hpBarGo);
        }

        GameObject RentHpBar()
        {
            GameObject hpBarGo = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.HpSlider);
            if (hpBarGo == null)
            {
                if (this.hpSliderTemplate == null)
                {
                    Debug.LogError("[BVBattleWorldUiComponent] HPSlider 未预加载。");
                    return null;
                }

                hpBarGo = Object.Instantiate(this.hpSliderTemplate, this.hpCanvasRect, false);
            }
            else
            {
                hpBarGo.transform.SetParent(this.hpCanvasRect, false);
            }

            RectTransform hpBarRect = hpBarGo.GetComponent<RectTransform>();
            if (hpBarRect != null)
            {
                hpBarRect.localScale = Vector3.one;
            }

            hpBarGo.SetActive(true);
            return hpBarGo;
        }

        static GameObject CreateBattleUiCanvas(string name, int sortOrder, Transform parent, Camera uiCamera)
        {
            GameObject go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = CanvasPlaneDistance;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = go.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            return go;
        }

        public void SyncHpBarWorldPosition(RectTransform hpBarRoot, Transform unitRoot, Vector3 localOffset)
        {
            if (hpBarRoot == null || unitRoot == null || this.hpCanvasRect == null)
            {
                return;
            }

            Vector3 worldPos = unitRoot.TransformPoint(localOffset);
            Vector2 localPos;
            if (this.TryWorldToCanvasLocal(worldPos, this.hpCanvasRect, out localPos))
            {
                hpBarRoot.anchoredPosition = localPos;
            }
        }

        public void PlayDamageFloat(Vector3 worldPosition, int damage)
        {
            if (this.damageCanvasRect == null || damage <= 0)
            {
                return;
            }

            Vector2 spawnLocalPos;
            if (!this.TryWorldToCanvasLocal(worldPosition, this.damageCanvasRect, out spawnLocalPos))
            {
                return;
            }

            GameObject floatGo = this.RentDamageFloat();
            if (floatGo == null)
            {
                return;
            }

            Text text = floatGo.GetComponent<Text>();
            RectTransform rect = floatGo.GetComponent<RectTransform>();
            if (text == null || rect == null)
            {
                Debug.LogError("[BVBattleWorldUiComponent] DamageFloatText 预制体缺少 Text 或 RectTransform。");
                this.ReturnDamageFloat(floatGo);
                return;
            }

            text.text = damage.ToString();
            text.raycastTarget = false;
            text.color = DamageFloatBaseColor;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = spawnLocalPos + DamageFloatLocalOffset;

            this.activeFloats.Add(new DamageFloatEntry
            {
                root = floatGo,
                text = text,
                rect = rect,
                remain = DamageFloatDuration,
                velocity = DamageFloatVelocity,
            });
        }

        GameObject RentDamageFloat()
        {
            this.EnsureWorldUiPoolsRegistered();
            if (this.damageCanvasRect == null)
            {
                return null;
            }

            GameObject floatGo = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DamageFloatText);
            if (floatGo == null)
            {
                if (this.damageFloatTemplate == null)
                {
                    Debug.LogError("[BVBattleWorldUiComponent] DamageFloatText 未预加载。");
                    return null;
                }

                floatGo = Object.Instantiate(this.damageFloatTemplate, this.damageCanvasRect, false);
            }
            else
            {
                floatGo.transform.SetParent(this.damageCanvasRect, false);
            }

            floatGo.SetActive(true);
            return floatGo;
        }

        void ReturnDamageFloat(GameObject floatGo)
        {
            if (floatGo == null)
            {
                return;
            }

            Text text = floatGo.GetComponent<Text>();
            if (text != null)
            {
                Color c = text.color;
                c.a = 1f;
                text.color = c;
            }

            floatGo.SetActive(false);
            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DamageFloatText, floatGo);
        }

        bool TryWorldToCanvasLocal(Vector3 worldPos, RectTransform canvasRect, out Vector2 localPos)
        {
            localPos = Vector2.zero;
            Camera worldCamera = Camera.main;
            Camera uiCamera = ViewManager.Instance != null ? ViewManager.Instance.GetUICamera() : null;
            if (worldCamera == null || uiCamera == null || canvasRect == null)
            {
                return false;
            }

            Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                uiCamera,
                out localPos);
        }

        public override void OnTick(float time)
        {
            if (this.activeFloats.Count == 0)
            {
                return;
            }

            for (int i = this.activeFloats.Count - 1; i >= 0; i--)
            {
                DamageFloatEntry e = this.activeFloats[i];
                e.remain -= time;
                e.rect.anchoredPosition += e.velocity * time;
                if (e.root == null)
                {
                    this.activeFloats.RemoveAt(i);
                    continue;
                }

                if (e.remain <= 0f)
                {
                    this.ReturnDamageFloat(e.root);
                    this.activeFloats.RemoveAt(i);
                }
                else
                {
                    if (e.text != null)
                    {
                        Color c = e.text.color;
                        c.a = Mathf.Clamp01(e.remain / DamageFloatDuration);
                        e.text.color = c;
                    }

                    this.activeFloats[i] = e;
                }
            }
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
