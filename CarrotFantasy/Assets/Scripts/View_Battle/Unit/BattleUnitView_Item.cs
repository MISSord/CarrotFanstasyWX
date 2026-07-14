using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Item : BattleUnitView
    {
        /// <summary>与旧版 ItemCanvas  anchoredPosition 一致。</summary>
        private static readonly Vector3 DefaultItemHpBarLocalOffset = new Vector3(-0.07f, 0.18f, 0f);

        private Slider slider;
        private GameObject hpBarInstance;
        private RectTransform hpBarRect;
        private Vector3 hpBarLocalOffset;
        private BVBattleWorldUiComponent worldUiCached;
        private bool hpBarCreated;
        private Item item;

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            this.item = this.transform.GetComponent<Item>();
            if (this.item != null)
            {
                this.item.itemView = this;
            }
        }

        public override void Init()
        {
            this.CacheWorldUi();
            base.Init();
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

        /// <summary>首次受击时由共享 Canvas 模板创建血条（与怪物一致）。</summary>
        public void AttachItemHpBar(GameObject hpBarRoot)
        {
            if (hpBarRoot == null)
            {
                Debug.LogError("[BattleUnitView_Item] 物品血条为空，请检查 HPSlider 预加载。");
                return;
            }

            this.hpBarInstance = hpBarRoot;
            this.hpBarRect = hpBarRoot.GetComponent<RectTransform>();
            this.slider = hpBarRoot.GetComponent<Slider>();
            if (this.slider == null)
            {
                Debug.LogError("[BattleUnitView_Item] 血条节点缺少 Slider 组件。");
                return;
            }

            this.slider.value = 1f;
            this.hpBarLocalOffset = DefaultItemHpBarLocalOffset;
            this.hpBarCreated = true;
            this.SyncHpBarPosition();
        }

        private void EnsureHpBar()
        {
            if (this.hpBarCreated || this.slider != null)
            {
                return;
            }

            if (this.worldUiCached == null)
            {
                this.CacheWorldUi();
            }

            if (this.worldUiCached == null)
            {
                Debug.LogError("[BattleUnitView_Item] 无法创建血条: WORLD_UI 未就绪。");
                return;
            }

            GameObject hpBarGo = this.worldUiCached.CreateHpBar();
            this.AttachItemHpBar(hpBarGo);
        }

        private void DestroyHpBar()
        {
            if (this.hpBarInstance == null)
            {
                this.hpBarRect = null;
                this.slider = null;
                this.hpBarCreated = false;
                return;
            }

            if (this.worldUiCached != null)
            {
                this.worldUiCached.ReturnHpBar(this.hpBarInstance);
            }
            else
            {
                Object.Destroy(this.hpBarInstance);
            }

            this.hpBarInstance = null;
            this.hpBarRect = null;
            this.slider = null;
            this.hpBarCreated = false;
        }

        public override void OnTick(float deltaTime)
        {
            base.OnTick(deltaTime);
            this.SyncHpBarPosition();
        }

        private void SyncHpBarPosition()
        {
            if (this.worldUiCached == null || this.hpBarRect == null || this.transform == null)
            {
                return;
            }

            this.worldUiCached.SyncHpBarWorldPosition(this.hpBarRect, this.transform, this.hpBarLocalOffset);
        }

        public override void InitListener()
        {
            base.InitListener();
            this.unitEventDispatcher.AddListener(BattleEvent.ITEM_LIVE_REDUCE, this.UpdateLiveNumber);
            this.unitEventDispatcher.AddListener<int>(BattleEvent.ITEM_DAMAGE_NUMBER, this.OnDamageNumber);
        }

        public override void RemoveListener()
        {
            base.RemoveListener();
            if (this.unitEventDispatcher != null)
            {
                this.unitEventDispatcher.RemoveListener(BattleEvent.ITEM_LIVE_REDUCE, this.UpdateLiveNumber);
                this.unitEventDispatcher.RemoveListener<int>(BattleEvent.ITEM_DAMAGE_NUMBER, this.OnDamageNumber);
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
            this.EnsureHpBar();
            if (this.slider == null)
            {
                return;
            }

            this.slider.value = ((float)((BattleUnit_Item)this.unit).curLive / (float)((BattleUnit_Item)this.unit).totalLive);
        }

        public void RefreshTarget()
        {
            this.battleView.battle.eventDispatcher.DispatchEvent<BattleUnit>(BattleEvent.TARGET_CHANGE, this.unit);
        }

        public override void ClearUnitInfo()
        {
            this.DestroyHpBar();
            this.worldUiCached = null;
            base.ClearUnitInfo();
            this.item = null;
        }
    }
}
