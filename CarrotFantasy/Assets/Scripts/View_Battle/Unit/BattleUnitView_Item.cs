using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BattleUnitView_Item : BattleUnitView
    {
        private Slider slider;
        private SpriteRenderer spriteRender;
        private Item item;

        public override void InitTransform(Transform node)
        {
            base.InitTransform(node);
            this.slider = this.transform.Find("ItemCanvas/HpSlider").GetComponent<Slider>();
            this.spriteRender = this.transform.GetComponent<SpriteRenderer>();
            this.slider.value = 1;

            this.slider.gameObject.SetActive(false);

            this.item = this.transform.GetComponent<Item>();
            this.item.itemView = this;
        }

        public override void InitListener()
        {
            base.InitListener();
            this.unitEventDispatcher.AddListener(BattleEvent.ITEM_LIVE_REDUCE, this.UpdateLiveNumber);
        }

        public override void RemoveListener()
        {
            base.RemoveListener();
            if (this.unitEventDispatcher != null)
            {
                this.unitEventDispatcher.RemoveListener(BattleEvent.ITEM_LIVE_REDUCE, this.UpdateLiveNumber);
            }
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
            base.ClearUnitInfo();
            this.slider = null;
            this.spriteRender = null;
        }
    }
}
