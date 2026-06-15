using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class ButtonTower
    {
        public int towerID;
        public int price;
        private Button button;
        private Sprite canClickSprite;
        private Sprite cantClickSprite;
        private Image image;

        private Transform transform;
        private BVUIComponent uiComponent;
        private int curPrice;

        public void InitInfo(Transform transform, int towerId)
        {
            this.transform = transform;
            this.towerID = towerId;

            this.image = this.transform.GetComponent<Image>();
            this.button = this.transform.GetComponent<Button>();
            this.EnsureSpritesLoaded();

            if (this.image != null && this.canClickSprite != null)
            {
                this.image.sprite = this.canClickSprite;
            }

            XUI.AddButtonListener(this.button, this.BuildTower);

            this.curPrice = (int)(TowerConfigReader.Instance.GetSingleTowerConfig(this.towerID)["price0"]);
        }

        public void LoadInfo(BVUIComponent baseView)
        {
            this.uiComponent = baseView;
        }

        private void EnsureSpritesLoaded()
        {
            if (this.canClickSprite == null)
            {
                if (!FightViewSpriteAb.TryGetTowerButton(this.towerID, true, out this.canClickSprite))
                {
                    Debug.LogError("[ButtonTower] CanClick1 未预加载: towerId=" + this.towerID);
                }
            }

            if (this.cantClickSprite == null)
            {
                if (!FightViewSpriteAb.TryGetTowerButton(this.towerID, false, out this.cantClickSprite))
                {
                    Debug.LogError("[ButtonTower] CanClick0 未预加载: towerId=" + this.towerID);
                }
            }
        }

        public void UpdateButtonSprite(int coin)
        {
            if (this.image == null)
            {
                return;
            }

            this.EnsureSpritesLoaded();
            if (coin >= this.curPrice)
            {
                if (this.canClickSprite != null)
                {
                    this.image.sprite = this.canClickSprite;
                }
            }
            else
            {
                if (this.cantClickSprite != null)
                {
                    this.image.sprite = this.cantClickSprite;
                }
            }
        }

        public void BuildTower()
        {
            if (this.uiComponent.selectGrid != null)
            {
                InputOrder curOrder = new InputOrder();
                curOrder.SetOrder(this.uiComponent.battle.curFrameId + 1,
                    this.uiComponent.selectGrid.mapGrid.x, this.uiComponent.selectGrid.mapGrid.y, InputOrderType.ADD_ORDER);
                curOrder.SetTowerId(this.towerID);

                ((BattleInputComponent)this.uiComponent.battle.GetComponent(BattleComponentType.InputComponent)).AddOrder(curOrder);
            }
            this.uiComponent.HandleGrid(this.uiComponent.selectGrid);
        }

        public void Dispose()
        {
            this.button.onClick.RemoveAllListeners();
        }
    }
}
