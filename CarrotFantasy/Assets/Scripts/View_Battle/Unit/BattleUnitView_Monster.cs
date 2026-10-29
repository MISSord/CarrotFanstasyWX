using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Monster : BattleUnitView
    {
        /// <summary>与 HPSlider 预制体 anchoredPosition 一致。</summary>
        private static readonly Vector3 DefaultMonsterHpBarLocalOffset = new Vector3(0f, 0.434f, 0f);

        private Slider slider;
        private GameObject hpBarCanvasInstance;
        private RectTransform hpBarCanvasRect;
        private Vector3 hpBarLocalOffset;
        private BVBattleWorldUiComponent worldUiCached;
        private readonly MonsterBuffIconBar buffIconBar = new MonsterBuffIconBar();
        private SpriteRenderer spriteRender;
        private SpriteLoader spriteLoader;
        private Animator animator;
        private static readonly Color NormalSpriteTint = Color.white;
        private static readonly Color StunSpriteTint = new Color(0.72f, 0.72f, 0.78f, 1f);

        /// <summary>异步加载控制器的版本号，避免对象池复用后旧回调覆盖新怪物的动画。</summary>
        private int animatorRequestVersion;

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            this.spriteRender = this.transform.GetComponent<SpriteRenderer>();
            this.spriteLoader = this.transform.GetComponent<SpriteLoader>();
            if (this.spriteLoader == null && this.spriteRender != null)
            {
                this.spriteLoader = this.transform.gameObject.AddComponent<SpriteLoader>();
            }

            this.animator = this.transform.GetComponent<Animator>();
        }

        /// <summary>由 <see cref="BVMonsterComponent"/> 在共享 BattleHpBarCanvas 下实例化 HPSlider。</summary>
        public void AttachMonsterHpBar(GameObject hpBarRoot)
        {
            if (hpBarRoot == null)
            {
                Debug.LogError("[BattleUnitView_Monster] 怪物血条为空，请检查 HPSlider 预加载与 BattleHpBarCanvas。");
                return;
            }

            this.hpBarCanvasInstance = hpBarRoot;
            this.hpBarCanvasRect = hpBarRoot.GetComponent<RectTransform>();
            this.slider = hpBarRoot.GetComponent<Slider>();
            if (this.slider == null)
            {
                Debug.LogError("[BattleUnitView_Monster] 血条节点缺少 Slider 组件。");
                return;
            }

            this.slider.value = 1;
            this.slider.gameObject.transform.eulerAngles = Vector3.zero;
            this.hpBarLocalOffset = DefaultMonsterHpBarLocalOffset;
            if (this.hpBarCanvasRect != null)
            {
                this.buffIconBar.Create(this.hpBarCanvasRect);
            }
        }

        public override void Init()
        {
            this.CacheWorldUi();
            base.Init();
            BattleUnit_Monster monster = (BattleUnit_Monster)this.unit;
            int bigLevel = monster.curLevel;
            string portraitAsset = FightViewSpriteAb.MonsterPortraitAssetName(bigLevel, monster.monsterId);
            string portraitBundle = FightViewSpriteAb.MonsterPortraitBundle(bigLevel, portraitAsset);
            if (this.spriteLoader != null)
            {
                this.spriteLoader.SetSprite(portraitBundle, portraitAsset);
            }
            else if (this.spriteRender != null)
            {
                this.spriteRender.sprite = null;
            }

            this.ApplyMonsterAnimator(monster);
        }

        /// <summary>
        /// 从 AB 加载怪物动画控制器。
        /// <para>
        /// 原实现用 Resources.Load("Animator/AnimatorController/Monster/{关卡}/{id}")，
        /// 但控制器在 <c>Assets/Game/Animator</c> 下，不在任何 Resources 目录里，永远取不到；
        /// 且打包规则此前显式排除 .controller，控制器根本没进 AB。
        /// </para>
        /// </summary>
        void ApplyMonsterAnimator(BattleUnit_Monster monster)
        {
            if (this.animator == null)
            {
                return;
            }

            int version = ++this.animatorRequestVersion;
            string bundle = FightViewSpriteAb.MonsterAnimatorBundle(monster.curLevel, monster.monsterId);
            string assetName = FightViewSpriteAb.MonsterAnimatorAssetName(monster.curLevel, monster.monsterId);

            AssetBundleManager.Instance.LoadAsset(
                bundle,
                assetName,
                typeof(RuntimeAnimatorController),
                obj =>
                {
                    // 对象池复用：期间可能已切到别的怪物，丢弃过期回调。
                    if (version != this.animatorRequestVersion || this.animator == null)
                    {
                        return;
                    }

                    RuntimeAnimatorController controller = obj as RuntimeAnimatorController;
                    if (controller == null)
                    {
                        return;
                    }

                    this.animator.runtimeAnimatorController = controller;
                    this.animator.Play(assetName);
                });
        }

        private void CacheWorldUi()
        {
            if (this.battleView == null)
            {
                this.worldUiCached = null;
                return;
            }

            BaseBattleViewComponent c = this.battleView.TryGetComponent(BattleViewComponentType.WORLD_UI);
            this.worldUiCached = c as BVBattleWorldUiComponent;
        }

        private void DetachHpBarFromWorldLayer()
        {
            if (this.hpBarCanvasInstance == null)
            {
                return;
            }

            if (this.worldUiCached != null)
            {
                this.worldUiCached.ReturnHpBar(this.hpBarCanvasInstance);
            }
            else
            {
                UnityEngine.Object.Destroy(this.hpBarCanvasInstance);
            }

            this.hpBarCanvasInstance = null;
            this.hpBarCanvasRect = null;
            this.slider = null;
        }

        public override void OnTick(float deltaTime)
        {
            base.OnTick(deltaTime);
            this.SyncHpBarPosition();
            this.buffIconBar.OnTick(deltaTime);
            this.buffIconBar.ResetPulsedIconScales();
        }

        private void SyncHpBarPosition()
        {
            if (this.worldUiCached == null || this.hpBarCanvasRect == null || this.transform == null)
            {
                return;
            }

            this.worldUiCached.SyncHpBarWorldPosition(this.hpBarCanvasRect, this.transform, this.hpBarLocalOffset);
        }

        public override void InitListener()
        {
            base.InitListener();
            this.unitEventDispatcher.AddListener(BattleEvent.MONSTER_LIVE_REDUCE, this.UpdateLiveNumber);
            this.unitEventDispatcher.AddListener<int>(BattleEvent.MONSTER_DAMAGE_NUMBER, this.OnDamageNumber);
            this.unitEventDispatcher.AddListener<BuffEventPayload>(UnitEvent.BUFF_ADD, this.OnBuffAdd);
            this.unitEventDispatcher.AddListener<BuffEventPayload>(UnitEvent.BUFF_REFRESH, this.OnBuffRefresh);
            this.unitEventDispatcher.AddListener<BuffEventPayload>(UnitEvent.BUFF_REMOVE, this.OnBuffRemove);
            this.unitEventDispatcher.AddListener(UnitEvent.STATUS_CHANGE, this.RefreshSpriteTint);
        }

        public override void RemoveListener()
        {
            base.RemoveListener();
            if (this.unitEventDispatcher != null)
            {
                this.unitEventDispatcher.RemoveListener(BattleEvent.MONSTER_LIVE_REDUCE, this.UpdateLiveNumber);
                this.unitEventDispatcher.RemoveListener<int>(BattleEvent.MONSTER_DAMAGE_NUMBER, this.OnDamageNumber);
                this.unitEventDispatcher.RemoveListener<BuffEventPayload>(UnitEvent.BUFF_ADD, this.OnBuffAdd);
                this.unitEventDispatcher.RemoveListener<BuffEventPayload>(UnitEvent.BUFF_REFRESH, this.OnBuffRefresh);
                this.unitEventDispatcher.RemoveListener<BuffEventPayload>(UnitEvent.BUFF_REMOVE, this.OnBuffRemove);
                this.unitEventDispatcher.RemoveListener(UnitEvent.STATUS_CHANGE, this.RefreshSpriteTint);
            }
        }

        private void OnBuffAdd(BuffEventPayload payload)
        {
            this.buffIconBar.ApplyOrRefresh(payload);
            this.RefreshSpriteTint();
        }

        private void OnBuffRefresh(BuffEventPayload payload)
        {
            this.buffIconBar.ApplyOrRefresh(payload);
        }

        private void OnBuffRemove(BuffEventPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            this.buffIconBar.Remove(payload.buffId);
            this.RefreshSpriteTint();
        }

        private void RefreshSpriteTint()
        {
            if (this.spriteRender == null)
            {
                return;
            }

            this.spriteRender.color = this.buffIconBar.HasCategory(BuffCategory.Stun)
                ? StunSpriteTint
                : NormalSpriteTint;
        }

        private void OnDamageNumber(int damage)
        {
            if (this.worldUiCached == null || this.transform == null)
            {
                return;
            }

            Vector3 p = this.transform.position;
            UnitTransformComponent t = this.unit != null
                ? (UnitTransformComponent)this.unit.GetComponent(UnitComponentType.TRANSFORM)
                : null;
            if (t != null)
            {
                p = new Vector3((float)t.lastFrameX, (float)t.lastFrameY, 0f);
            }

            this.worldUiCached.PlayDamageFloat(p, damage);
        }

        private void UpdateLiveNumber()
        {
            if (this.slider == null) return;
            this.slider.value = ((float)((BattleUnit_Monster)this.unit).curLive / (float)((BattleUnit_Monster)this.unit).totalLive);
        }

        protected override void UpdateFaceDirection()
        {
            base.UpdateFaceDirection();
            if (this.slider != null)
            {
                this.slider.gameObject.transform.eulerAngles = Vector3.zero;
            }
        }

        public override void ClearUnitInfo()
        {
            this.buffIconBar.ClearAll();
            this.DetachHpBarFromWorldLayer();
            this.worldUiCached = null;
            if (this.spriteLoader != null)
            {
                this.spriteLoader.ReleaseCurrent();
            }

            base.ClearUnitInfo();
            this.animator = null;
            this.spriteRender = null;
            this.spriteLoader = null;
        }
    }
}
