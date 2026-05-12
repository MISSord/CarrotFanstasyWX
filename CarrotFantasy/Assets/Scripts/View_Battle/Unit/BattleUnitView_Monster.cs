using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Monster : BattleUnitView
    {
        /// <summary>与旧版嵌套 MonsterCanvas 时一致的头顶偏移（怪物本地空间）。</summary>
        private static readonly Vector3 DefaultMonsterHpBarLocalOffset = new Vector3(0f, 0.434f, 0f);

        private Slider slider;
        private GameObject hpBarCanvasInstance;
        private RectTransform hpBarCanvasRect;
        private Vector3 hpBarLocalOffset;
        private BVBattleWorldUiComponent worldUiCached;
        private SpriteRenderer spriteRender;
        private Animator animator;

        private String spriteUrl = "Pictures/NormalMordel/Game/{0}/Monster/{1}-1";
        private String animatorUrl = "Animator/AnimatorController/Monster/{0}/{1}";

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            this.spriteRender = this.transform.GetComponent<SpriteRenderer>();
            this.animator = this.transform.GetComponent<Animator>();
        }

        /// <summary>由 <see cref="BVMonsterComponent"/> 在加载独立 MonsterCanvas 预制体后调用。</summary>
        public void AttachMonsterHpCanvas(GameObject monsterCanvasInstance)
        {
            if (monsterCanvasInstance == null)
            {
                Debug.LogError("[BattleUnitView_Monster] MonsterCanvas 实例为空，请检查 fightpart_prefab 中 MonsterCanvas 资源。");
                return;
            }

            this.hpBarCanvasInstance = monsterCanvasInstance;
            this.hpBarCanvasRect = monsterCanvasInstance.GetComponent<RectTransform>();
            Transform sliderTr = monsterCanvasInstance.transform.Find("HPSlider");
            if (sliderTr == null)
            {
                Debug.LogError("[BattleUnitView_Monster] MonsterCanvas 下缺少 HPSlider。");
                return;
            }

            this.slider = sliderTr.GetComponent<Slider>();
            this.slider.value = 1;
            this.slider.gameObject.transform.eulerAngles = Vector3.zero;

            this.hpBarLocalOffset = DefaultMonsterHpBarLocalOffset;
        }

        public override void Init()
        {
            this.CacheWorldUi();
            this.AttachHpBarToWorldLayer();
            base.Init();
            BattleUnit_Monster monster = (BattleUnit_Monster)this.unit;
            this.spriteRender.sprite = ResourceLoader.Instance.loadRes<Sprite>(
                String.Format(this.spriteUrl, monster.curLevel, monster.monsterId));
            this.animator.runtimeAnimatorController = ResourceLoader.Instance.loadRes<RuntimeAnimatorController>(
                String.Format(this.animatorUrl, monster.curLevel, monster.monsterId));
            this.animator.Play(monster.monsterId.ToString());
        }

        private void CacheWorldUi()
        {
            if (this.battleView == null)
            {
                this.worldUiCached = null;
                return;
            }

            BaseBattleViewComponent c = this.battleView.GetComponent(BattleViewComponentType.WORLD_UI);
            this.worldUiCached = c as BVBattleWorldUiComponent;
        }

        private void AttachHpBarToWorldLayer()
        {
            if (this.worldUiCached != null && this.hpBarCanvasRect != null)
            {
                this.worldUiCached.AttachHpBarToSharedCanvas(this.hpBarCanvasRect);
            }
        }

        private void DetachHpBarFromWorldLayer()
        {
            if (this.hpBarCanvasInstance != null)
            {
                UnityEngine.Object.Destroy(this.hpBarCanvasInstance);
                this.hpBarCanvasInstance = null;
            }

            this.hpBarCanvasRect = null;
            this.slider = null;
        }

        public override void OnTick(float deltaTime)
        {
            base.OnTick(deltaTime);
            this.SyncHpBarPosition();
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
        }

        public override void RemoveListener()
        {
            base.RemoveListener();
            if (this.unitEventDispatcher != null)
            {
                this.unitEventDispatcher.RemoveListener(BattleEvent.MONSTER_LIVE_REDUCE, this.UpdateLiveNumber);
                this.unitEventDispatcher.RemoveListener<int>(BattleEvent.MONSTER_DAMAGE_NUMBER, this.OnDamageNumber);
            }
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
            this.DetachHpBarFromWorldLayer();
            this.worldUiCached = null;
            base.ClearUnitInfo();
            this.animator = null;
            this.spriteRender = null;
        }
    }
}
