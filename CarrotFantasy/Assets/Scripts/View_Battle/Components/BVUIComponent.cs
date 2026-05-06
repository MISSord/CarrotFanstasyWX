using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BVUIComponent : BaseBattleViewComponent
    {
        public GridPoint selectGrid { get; private set; }//上一个选择的格子

        private GameObject nodeHandleTowerCanvas;
        private int towerLength;

        private BattleTowerComponent towerComponent;
        private BattleDataComponent dataComponent;
        private BattleMapComponent mapComponent;

        private GameObject nodeTowerList;

        private ButtonTower[] buttonTowerList;

        private Vector3 upLevelButtonInitPos;//两个按钮的初始位置
        private Vector3 sellTowerButtonInitPos;

        private Sprite[] spriteButtonUpList;

        private Transform tranButtonUp;//两个按钮的trans引用
        private Transform tranButtonSell;

        private Image imgButtonUp;
        private Text txtButtonUp;

        private GameObject nodeMap;
        private GameObject nodeTargetSignal;

        private BattleUnit tranTarget;

        private Text txtButtonSell;

        private MapUIConfigReader reader;

        private GameObject nodeCarrot;
        private GameObject nodeMonsterPoint;
        private Carrot carrot;

        private GameObject rootGameObject;

        private readonly List<AssetLoadHandle> _prefabHandles = new List<AssetLoadHandle>();

        public BVUIComponent(BattleView_base battleView) : base(battleView)
        {
            this.towerComponent = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            this.dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.mapComponent = (BattleMapComponent)this.battle.GetComponent(BattleComponentType.MapComponent);
            this.buttonTowerList = new ButtonTower[this.towerComponent.canBuildTowerListLength];
            this.spriteButtonUpList = new Sprite[3];
            this.componentType = BattleViewComponentType.UI;

            this.reader = new MapUIConfigReader();
            this.reader.Init();
        }

        private void AddListener()
        {
            this.battleView.bvEventDispatcher.AddListener<GridPoint>(BattleViewEventType.Select_Grid, this.HandleGrid);

            this.battleView.bvEventDispatcher.AddListener<GridPoint>(BattleViewEventType.Show_Handle_Tower, this.ShowHandleTowerCanvas);
            this.battleView.bvEventDispatcher.AddListener(BattleViewEventType.Fade_Handle_Tower, this.FadeHandleTowerCanvas);
            this.battleView.bvEventDispatcher.AddListener<GridPoint>(BattleViewEventType.Show_Tower_List, this.ShowTowerList);
            this.battleView.bvEventDispatcher.AddListener(BattleViewEventType.Fade_Tower_List, this.FadeTowerList);

            this.eventDispatcher.AddListener<int>(BattleEvent.COIN_CHANGE, this.RefreshButtonInfo);

            XUI.AddButtonListener(this.tranButtonSell.GetComponent<Button>(), this.SellTower);
            XUI.AddButtonListener(this.tranButtonUp.GetComponent<Button>(), this.UpdateTower);

            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.UpdateNodeState);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.UpdateTargetSignal);

            this.eventDispatcher.AddListener<BattleUnit>(BattleEvent.TARGET_CHANGE, this.SetTargetSignal);
        }

        private GameObject LoadPrefabTemplateTracked(string bundleName, string assetName)
        {
            GameObject tpl = GameObjectResourceManager.Instance.LoadPrefabBlocking(bundleName, assetName, out AssetLoadHandle h);
            if (h.IsValid)
            {
                _prefabHandles.Add(h);
            }

            if (tpl == null)
            {
                Debug.LogError($"[BVUIComponent] 预制体加载失败: bundle={bundleName}, asset={assetName}");
            }

            return tpl;
        }

        private void RemoveListener()
        {
            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Select_Grid, this.HandleGrid);

            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Show_Handle_Tower, this.ShowHandleTowerCanvas);
            this.battleView.bvEventDispatcher.RemoveListener(BattleViewEventType.Fade_Handle_Tower, this.FadeHandleTowerCanvas);
            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Show_Tower_List, this.ShowTowerList);
            this.battleView.bvEventDispatcher.RemoveListener(BattleViewEventType.Fade_Tower_List, this.FadeTowerList);

            this.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.RefreshButtonInfo);

            this.tranButtonSell.GetComponent<Button>().onClick.RemoveAllListeners();
            this.tranButtonUp.GetComponent<Button>().onClick.RemoveAllListeners();

            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.UpdateNodeState);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.UpdateTargetSignal);

            this.eventDispatcher.RemoveListener<BattleUnit>(BattleEvent.TARGET_CHANGE, this.SetTargetSignal);
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("UIContainer");

            GameObject tplTowerList = LoadPrefabTemplateTracked(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.TowerList);
            if (tplTowerList == null)
            {
                return;
            }

            this.nodeTowerList = GameObject.Instantiate(tplTowerList);
            this.nodeTowerList.transform.SetParent(this.rootGameObject.transform);
            this.nodeTowerList.transform.position = this.battleView.initTran;
            this.nodeTowerList.transform.GetComponent<Canvas>().sortingOrder = 20;

            GameObject tplBtnTower = LoadPrefabTemplateTracked(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.BtnTowerBuild);
            if (tplBtnTower == null)
            {
                return;
            }

            for (int i = 0; i <= this.towerComponent.canBuildTowerListLength - 1; i++)
            {
                GameObject itemGo = GameObject.Instantiate(tplBtnTower);
                this.buttonTowerList[i] = new ButtonTower();
                this.buttonTowerList[i].LoadInfo(this);
                this.buttonTowerList[i].InitInfo(itemGo.transform, this.towerComponent.canBuildTowerList[i]);

                itemGo.transform.SetParent(this.nodeTowerList.transform);
                itemGo.transform.localPosition = Vector3.zero;
                itemGo.transform.localScale = Vector3.one;
            }

            GameObject tplHandleCanvas = LoadPrefabTemplateTracked(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.HandleTowerCanvas);
            if (tplHandleCanvas == null)
            {
                return;
            }

            this.nodeHandleTowerCanvas = GameObject.Instantiate(tplHandleCanvas);
            this.nodeHandleTowerCanvas.transform.SetParent(this.rootGameObject.transform);
            this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
            this.nodeHandleTowerCanvas.transform.GetComponent<Canvas>().sortingOrder = 20;

            this.spriteButtonUpList[0] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Tower/Btn_CantUpLevel"); //不能升级
            this.spriteButtonUpList[1] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Tower/Btn_CanUpLevel"); //能升级
            this.spriteButtonUpList[2] = ResourceLoader.Instance.loadRes<Sprite>("Pictures/NormalMordel/Game/Tower/Btn_ReachHighestLevel"); //满级

            GameObject tplNodeMap = LoadPrefabTemplateTracked(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeMap);
            if (tplNodeMap == null)
            {
                return;
            }

            this.nodeMap = GameObject.Instantiate(tplNodeMap);
            this.nodeMap.transform.SetParent(this.rootGameObject.transform);
            this.nodeMap.transform.position = new Vector3(6, 4.35f, 0);
            Dictionary<String, int> map = this.reader.getMapUIConfig(dataComponent.bigLevel, dataComponent.level);
            this.nodeMap.transform.Find("img_bg").GetComponent<SpriteRenderer>().sprite = ResourceLoader.Instance.
                loadRes<Sprite>(String.Format("Pictures/NormalMordel/Game/{0}/BG{1}", dataComponent.bigLevel, map["mapBg"]));
            this.nodeMap.transform.Find("img_road").GetComponent<SpriteRenderer>().sprite = ResourceLoader.Instance.
                loadRes<Sprite>(String.Format("Pictures/NormalMordel/Game/{0}/Road{1}", dataComponent.bigLevel, map["mapRoad"]));
            this.nodeMap.transform.Find("img_bg").GetComponent<SpriteRenderer>().sortingOrder = 0;
            this.nodeMap.transform.Find("img_road").GetComponent<SpriteRenderer>().sortingOrder = 4;

            GameObject tplTargetSignal = LoadPrefabTemplateTracked(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeTargetSignal);
            if (tplTargetSignal == null)
            {
                return;
            }

            this.nodeTargetSignal = GameObject.Instantiate(tplTargetSignal);
            this.nodeTargetSignal.transform.SetParent(this.rootGameObject.transform);
            this.nodeTargetSignal.transform.position = this.battleView.initTran;

            this.LoadInfo();
            this.SetStartPoint();
            this.SetCarrot();

            this.AddListener();
        }

        private void SetStartPoint()
        {
            GameObject tplStart = LoadPrefabTemplateTracked(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.StartPoint);
            if (tplStart == null)
            {
                return;
            }

            this.nodeMonsterPoint = GameObject.Instantiate(tplStart);
            this.nodeMonsterPoint.transform.SetParent(this.rootGameObject.transform);
            Fix64Vector2 startPosition = this.mapComponent.monsterPathList[0];

            bool isRight = this.mapComponent.monsterPathList[1].X - this.mapComponent.monsterPathList[0].X > Fix64.Zero ? true : false;
            bool isUP = this.mapComponent.monsterPathList[1].Y - this.mapComponent.monsterPathList[0].Y > Fix64.Zero ? true : false;

            if (this.mapComponent.monsterPathList[1].X - this.mapComponent.monsterPathList[0].X != Fix64.Zero) //左或者右
            {
                if (isRight == true)
                {
                    this.nodeMonsterPoint.transform.position = new Vector3((float)startPosition.X, (float)startPosition.Y + 0.5f, 0);
                }
                else
                {
                    this.nodeMonsterPoint.transform.position = new Vector3((float)startPosition.X, (float)startPosition.Y + 0.3f, 0);
                }
            }
            else //上或下
            {
                if (isUP == true)
                {
                    this.nodeMonsterPoint.transform.position = new Vector3((float)startPosition.X - 0.1f, (float)startPosition.Y - 0.5f, 0);
                }
                else
                {
                    this.nodeMonsterPoint.transform.position = new Vector3((float)startPosition.X - 0.1f, (float)startPosition.Y + 0.5f, 0);
                }
            }
        }

        private void SetCarrot()
        {
            GameObject tplCarrot = LoadPrefabTemplateTracked(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.Carrot);
            if (tplCarrot == null)
            {
                return;
            }

            this.nodeCarrot = GameObject.Instantiate(tplCarrot);
            this.nodeCarrot.transform.SetParent(this.rootGameObject.transform);
            this.carrot = this.nodeCarrot.transform.GetComponent<Carrot>();
            this.carrot.Init();
            Fix64Vector2 endPosition = this.mapComponent.monsterPathList[this.mapComponent.monsterPathList.Count - 1];
            this.carrot.transform.position = new Vector3((float)endPosition.X + 0.1f, (float)endPosition.Y + 0.5f, 0);
        }

        private void LoadInfo()
        {
            tranButtonUp = this.nodeHandleTowerCanvas.transform.Find("btn_up_level");
            tranButtonSell = this.nodeHandleTowerCanvas.transform.Find("btn_sell");

            this.imgButtonUp = this.nodeHandleTowerCanvas.transform.Find("btn_up_level").GetComponent<Image>();
            this.txtButtonUp = this.nodeHandleTowerCanvas.transform.Find("btn_up_level/txt_price").GetComponent<Text>();

            this.txtButtonSell = this.nodeHandleTowerCanvas.transform.Find("btn_sell/txt_price").GetComponent<Text>();

            this.upLevelButtonInitPos = tranButtonUp.localPosition;
            this.sellTowerButtonInitPos = tranButtonSell.localPosition;
        }

        private void ShowHandleTowerCanvas(GridPoint grid)
        {
            this.selectGrid = grid;
            this.nodeHandleTowerCanvas.transform.position = new Vector3((float)grid.mapGrid.realX, (float)grid.mapGrid.realY, 0);
            this.battleView.bvEventDispatcher.DispatchEvent<GridPoint>(BattleEvent.TOWER_RANGE_SHOW, grid);
            this.CorrectHandleTowerCanvasGoPosition(grid);
            this.RefreshButtonInfo(0);
        }

        private void RefreshButtonInfo(int coin)
        {
            for (int i = 0; i < this.buttonTowerList.Length; i++)
            {
                this.buttonTowerList[i].UpdateButtonSprite(dataComponent.CoinCount);
            }
            if (this.selectGrid == null) return;
            BattleUnit_Tower tower = towerComponent.GetTowerInfo(this.selectGrid.mapGrid.x, this.selectGrid.mapGrid.y);
            if (tower == null) return;
            if (tower.isMaxLevel == true)
            {
                this.imgButtonUp.sprite = this.spriteButtonUpList[2];
                this.txtButtonUp.text = "";
            }
            else
            {
                if (dataComponent.CoinCount >= tower.price[tower.curLevel + 1])
                {
                    this.imgButtonUp.sprite = this.spriteButtonUpList[1];
                }
                else
                {
                    this.imgButtonUp.sprite = this.spriteButtonUpList[0];
                }
                this.txtButtonUp.text = tower.price[tower.curLevel + 1].ToString();
            }
            this.txtButtonSell.text = (tower.price[tower.curLevel] - 20).ToString();
        }

        //纠正操作塔UI画布的方法(纠正按钮位置的方法)
        private void CorrectHandleTowerCanvasGoPosition(GridPoint grid)
        {
            tranButtonUp.localPosition = Vector3.zero;
            tranButtonSell.localPosition = Vector3.zero;
            if (grid.mapGrid.y <= 0)
            {
                if (grid.mapGrid.x == 0)
                {
                    tranButtonSell.position += new Vector3(BattleConfig.MAP_RATIO * 3 / 4, 0, 0);
                }
                else
                {
                    tranButtonSell.position -= new Vector3(BattleConfig.MAP_RATIO * 3 / 4, 0, 0);
                }
                tranButtonUp.localPosition = upLevelButtonInitPos;
            }
            else if (grid.mapGrid.y >= 6)
            {
                if (grid.mapGrid.x == 0)
                {
                    tranButtonUp.position += new Vector3(BattleConfig.MAP_RATIO * 3 / 4, 0, 0);
                }
                else
                {
                    tranButtonUp.position -= new Vector3(BattleConfig.MAP_RATIO * 3 / 4, 0, 0);
                }
                tranButtonSell.localPosition = sellTowerButtonInitPos;
            }
            else
            {
                tranButtonUp.localPosition = upLevelButtonInitPos;
                tranButtonSell.localPosition = sellTowerButtonInitPos;
            }
        }

        private void FadeHandleTowerCanvas()
        {
            this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
            this.battleView.bvEventDispatcher.DispatchEvent(BattleEvent.TOWER_RANGE_FADE);
        }

        private void ShowTowerList(GridPoint grid)
        {
            this.nodeTowerList.transform.position = new Vector3((float)grid.mapGrid.realX, (float)grid.mapGrid.realY, 0);
            this.nodeTowerList.transform.position += this.CorrectTowerListGoPosition(grid);
        }

        private Vector3 CorrectTowerListGoPosition(GridPoint grid)
        {
            Vector3 correctPosition = Vector3.zero;
            if (grid.mapGrid.x <= 3 && grid.mapGrid.x >= 0)
            {
                correctPosition += new Vector3(BattleConfig.MAP_RATIO, 0, 0);
            }
            else if (grid.mapGrid.x <= 11 && grid.mapGrid.x >= 8)
            {
                correctPosition -= new Vector3(BattleConfig.MAP_RATIO, 0, 0);
            }
            if (grid.mapGrid.y <= 3 && grid.mapGrid.y >= 0)
            {
                correctPosition += new Vector3(0, BattleConfig.MAP_RATIO, 0);
            }
            else if (grid.mapGrid.y <= 7 && grid.mapGrid.y >= 4)
            {
                correctPosition -= new Vector3(0, BattleConfig.MAP_RATIO, 0);
            }
            return correctPosition;
        }

        private void FadeTowerList()
        {
            this.nodeTowerList.transform.position = this.battleView.initTran;
        }

        public void HandleGrid(GridPoint grid)//当前选择的格子
        {
            if (grid.mapGrid.state.canBuild)
            {
                if (selectGrid == null)//没有上一个格子
                {
                    selectGrid = grid;
                    selectGrid.ShowGrid();
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/GridSelect");
                }
                else if (grid == selectGrid)//选中同一个格子
                {
                    grid.HideGrid();
                    selectGrid = null;
                    this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/GridDeselect");
                }
                else if (grid != selectGrid)//选中不同格子
                {
                    selectGrid.HideGrid();
                    selectGrid = grid;
                    selectGrid.ShowGrid();
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/GridSelect");
                }
            }
            else
            {
                grid.HideGrid();
                grid.ShowCantBuild();
                this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
                AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/SelectFault");
                if (selectGrid != null)
                {
                    selectGrid.HideGrid();
                }
            }
        }

        private void UpdateTower()
        {
            if (this.selectGrid == null)
            {
                Debug.Log("没有当前格子，无法升级");
                return;
            }
            BattleUnit_Tower tower = towerComponent.GetTowerInfo(this.selectGrid.mapGrid.x, this.selectGrid.mapGrid.y);
            if (tower.isMaxLevel == true) return;
            InputOrder order = new InputOrder();
            order.SetOrder(this.battle.curFrameId + 1, this.selectGrid.mapGrid.x, this.selectGrid.mapGrid.y, InputOrderType.UPDATE_ORDER);
            ((BattleInputComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.InputComponent)).AddOrder(order);
            this.selectGrid.HideGrid();
        }

        private void SellTower()
        {
            if (this.selectGrid == null)
            {
                Debug.Log("没有当前格子，无法出售");
                return;
            }
            InputOrder order = new InputOrder();
            order.SetOrder(this.battle.curFrameId + 1, this.selectGrid.mapGrid.x, this.selectGrid.mapGrid.y, InputOrderType.REMOVE_ORDER);
            ((BattleInputComponent)BattleManager.Instance.baseBattle.GetComponent(BattleComponentType.InputComponent)).AddOrder(order);
            this.selectGrid.HideGrid();
        }

        private void UpdateNodeState(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER))
            {
                this.FadeHandleTowerCanvas();
                this.FadeTowerList();
                if (this.selectGrid != null)
                {
                    this.selectGrid.HideGrid();
                    this.selectGrid = null;
                }
            }
        }

        private void UpdateTargetSignal(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.ITEM))
            {
                if (unit == this.tranTarget)
                {
                    this.FadeTargetSignal();
                }
            }
        }

        private void SetTargetSignal(BattleUnit unit)
        {
            if (this.tranTarget == null)
            {
                this.tranTarget = unit;
                this.ShowTargetSignal();
            }
            //转换新目标
            else if (this.tranTarget != unit)
            {
                this.tranTarget = unit;
                this.ShowTargetSignal();
            }
            //两次点击的是同一个目标
            else if (this.tranTarget == unit)
            {
                this.tranTarget = null;
                this.FadeTargetSignal();
            }
        }

        public override void ClearGameInfo()
        {
            if (this.carrot != null)
            {
                this.carrot.Dispose();
            }

            for (int i = 0; i < this.buttonTowerList.Length - 1; i++)
            {
                this.buttonTowerList[i].Dispose();
            }

            this.RemoveListener();
            this.selectGrid = null;
            if (this.nodeHandleTowerCanvas != null)
            {
                GameObject.Destroy(this.nodeHandleTowerCanvas);
            }

            if (this.nodeTowerList != null)
            {
                GameObject.Destroy(this.nodeTowerList);
            }

            if (this.nodeCarrot != null)
            {
                GameObject.Destroy(this.nodeCarrot);
            }

            if (this.nodeMap != null)
            {
                GameObject.Destroy(this.nodeMap);
            }

            if (this.nodeMonsterPoint != null)
            {
                GameObject.Destroy(this.nodeMonsterPoint);
            }

            if (this.nodeTargetSignal != null)
            {
                GameObject.Destroy(this.nodeTargetSignal);
            }

            for (int i = 0; i < _prefabHandles.Count; i++)
            {
                _prefabHandles[i].Dispose();
            }

            _prefabHandles.Clear();
        }

        private void ShowTargetSignal()
        {
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/ShootSelect");
            UnitTransformComponent tranComponent = (UnitTransformComponent)this.tranTarget.GetComponent(UnitComponentType.TRANSFORM);
            Fix64Vector2 pos = tranComponent.GetLastPosition();
            Vector3 position = new Vector3((float)pos.X, (float)pos.Y, 0);
            this.nodeTargetSignal.transform.position = position + new Vector3(0, BattleConfig.MAP_RATIO / 2, 0);
            //nodeTargetSignal.transform.SetParent(targetTrans);
        }

        private void FadeTargetSignal()
        {
            this.nodeTargetSignal.transform.position = this.battleView.initTran;
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }

    }
}
