using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Item : BattleUnitView
    {
        private Slider slider;
        private RectTransform hpBarCanvasRect;
        private Vector3 hpBarLocalOffset;
        private BVBattleWorldUiComponent worldUiCached;
        private SpriteRenderer spriteRender;
        private Item item;

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            Transform itemCanvas = this.transform.Find("ItemCanvas");
            if (itemCanvas != null)
            {
                this.hpBarCanvasRect = itemCanvas.GetComponent<RectTransform>();
                this.hpBarLocalOffset = this.hpBarCanvasRect.localPosition;
            }

            this.slider = this.transform.Find("ItemCanvas/HpSlider").GetComponent<Slider>();
            this.spriteRender = this.transform.GetComponent<SpriteRenderer>();
            this.slider.value = 1;

            this.slider.gameObject.SetActive(false);

            this.item = this.transform.GetComponent<Item>();
            this.item.itemView = this;
        }

        public override void Init()
        {
            this.CacheWorldUi();
            this.AttachHpBarToWorldLayer();
            base.Init();
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
            if (this.worldUiCached != null && this.hpBarCanvasRect != null && this.transform != null)
            {
                this.worldUiCached.DetachHpBarToUnit(this.hpBarCanvasRect, this.transform);
            }
            else if (this.hpBarCanvasRect != null && this.transform != null)
            {
                this.hpBarCanvasRect.SetParent(this.transform, worldPositionStays: true);
            }
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

            if (this.hpBarCanvasRect.parent == this.transform)
            {
                return;
            }

            this.worldUiCached.SyncHpBarWorldPosition(this.hpBarCanvasRect, this.transform, this.hpBarLocalOffset);
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
            if (this.slider.gameObject.activeSelf == false) this.slider.gameObject.SetActive(true);
            this.slider.value = ((float)((BattleUnit_Item)this.unit).curLive / (float)((BattleUnit_Item)this.unit).totalLive);
        }

        public void RefreshTarget()
        {
            this.battleView.battle.eventDispatcher.DispatchEvent<BattleUnit>(BattleEvent.TARGET_CHANGE, this.unit);
        }

        public override void ClearUnitInfo()
        {
            this.DetachHpBarFromWorldLayer();
            this.worldUiCached = null;
            base.ClearUnitInfo();
            this.slider = null;
            this.hpBarCanvasRect = null;
            this.spriteRender = null;
        }
    }
}
