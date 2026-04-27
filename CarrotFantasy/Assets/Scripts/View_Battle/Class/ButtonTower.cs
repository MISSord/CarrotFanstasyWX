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
            this.canClickSprite = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Tower/" + towerID.ToString() + "/CanClick1");
            this.cantClickSprite = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Tower/" + towerID.ToString() + "/CanClick0");

            this.image = this.transform.GetComponent<Image>();
            this.button = this.transform.GetComponent<Button>();

            this.image.sprite = this.canClickSprite;
            this.button.onClick.AddListener(this.BuildTower);

            this.curPrice = (int)(TowerConfigReader.Instance.GetSingleTowerConfig(this.towerID)["price0"]);
        }

        public void LoadInfo(BVUIComponent baseView)
        {
            this.uiComponent = baseView;
        }

        public void UpdateButtonSprite(int coin)
        {
            if (coin >= this.curPrice)
            {
                this.image.sprite = this.canClickSprite;
            }
            else
            {
                this.image.sprite = this.cantClickSprite;
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

                ((BattleInputComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.InputComponent)).AddOrder(curOrder);
            }
            this.uiComponent.HandleGrid(this.uiComponent.selectGrid);
        }

        public void Dispose()
        {
            this.button.onClick.RemoveAllListeners();
        }
    }
}
