using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    /// <summary>
    /// 战斗 HUD 组件：格子选中、建塔/升级/出售面板、小地图、萝卜、出怪点与道具目标指示。
    /// 生命周期与 <see cref="BaseBattleViewComponent"/> 一致；UI 节点在 <see cref="Init"/> 中一次性创建，
    /// 同关重开走 <see cref="ResetRound"/> 复位位置与绑定，不销毁预制体。
    /// </summary>
    public class BVUIComponent : BaseBattleViewComponent
    {
        /// <summary>当前选中的可建造格子；为 null 表示无展开状态。</summary>
        public GridPoint selectGrid { get; private set; }

        /// <summary>升级/出售面板；隐藏时移回 <see cref="BattleView_base.initTran"/>。</summary>
        private GameObject nodeHandleTowerCanvas;

        private BattleTowerComponent towerComponent;
        private BattleDataComponent dataComponent;
        private BattlePVEDataComponent pveDataComponent;
        private BattleMapComponent mapComponent;
        private BattlePVEMapComponent pveMapComponent;

        private GameObject nodeTowerList;

        private ButtonTower[] buttonTowerList;

        private Vector3 upLevelButtonInitPos;
        private Vector3 sellTowerButtonInitPos;

        private Sprite[] spriteButtonUpList;

        private Transform tranButtonUp;
        private Transform tranButtonSell;

        private Image imgButtonUp;
        private Text txtButtonUp;

        private GameObject nodeMap;
        private GameObject nodeTargetSignal;

        /// <summary>当前道具攻击目标；与 <see cref="nodeTargetSignal"/> 联动。</summary>
        private BattleUnit tranTarget;

        private Text txtButtonSell;

        private GameObject nodeCarrot;
        private GameObject nodeMonsterPoint;
        private Carrot carrot;

        private GameObject rootGameObject;

        /// <summary>UI 预制体是否已在本次 Session 中实例化；重开时不重置，仅 <see cref="ClearGameInfo"/> 清零。</summary>
        private bool uiContentBuilt;

        public BVUIComponent(BattleView_base battleView) : base(battleView)
        {
            this.towerComponent = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            this.dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.pveDataComponent = BattlePVEDataComponent.GetFrom(this.battle);
            this.mapComponent = (BattleMapComponent)this.battle.GetComponent(BattleComponentType.MapComponent);
            this.pveMapComponent = BattlePVEMapComponent.GetFrom(this.battle);
            this.buttonTowerList = new ButtonTower[this.towerComponent.canBuildTowerListLength];
            this.spriteButtonUpList = new Sprite[3];
            this.componentType = BattleViewComponentType.UI;
        }

        private void AddListener()
        {
            // View 层格子交互走 bvEventDispatcher；Model 层金币/单位/目标走 battle.eventDispatcher。
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

        private GameObject GetPrefabTemplate(string bundleName, string assetName)
        {
            GameObject tpl;
            if (BattleViewPrefabPreloader.TryGetTemplate(bundleName, assetName, out tpl))
            {
                return tpl;
            }

            Debug.LogError($"[BVUIComponent] 预制体未预加载: bundle={bundleName}, asset={assetName}");
            return null;
        }

        private void RemoveListener()
        {
            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Select_Grid, this.HandleGrid);

            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Show_Handle_Tower, this.ShowHandleTowerCanvas);
            this.battleView.bvEventDispatcher.RemoveListener(BattleViewEventType.Fade_Handle_Tower, this.FadeHandleTowerCanvas);
            this.battleView.bvEventDispatcher.RemoveListener<GridPoint>(BattleViewEventType.Show_Tower_List, this.ShowTowerList);
            this.battleView.bvEventDispatcher.RemoveListener(BattleViewEventType.Fade_Tower_List, this.FadeTowerList);

            this.eventDispatcher.RemoveListener<int>(BattleEvent.COIN_CHANGE, this.RefreshButtonInfo);

            if (this.tranButtonSell != null)
            {
                Button sellButton = this.tranButtonSell.GetComponent<Button>();
                if (sellButton != null)
                {
                    sellButton.onClick.RemoveAllListeners();
                }
            }

            if (this.tranButtonUp != null)
            {
                Button upButton = this.tranButtonUp.GetComponent<Button>();
                if (upButton != null)
                {
                    upButton.onClick.RemoveAllListeners();
                }
            }

            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.UpdateNodeState);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.UpdateTargetSignal);

            this.eventDispatcher.RemoveListener<BattleUnit>(BattleEvent.TARGET_CHANGE, this.SetTargetSignal);
        }

        public override void Init()
        {
            if (this.uiContentBuilt)
            {
                return;
            }

            // 以下节点挂到 Scene 的 UIContainer，sortingOrder=20 的 Canvas 需盖住地图层。
            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVUIComponent] BVSceneComponent 未注册。");
                return;
            }

            this.rootGameObject = scene.RegisterGameContainer("UIContainer");
            if (this.rootGameObject == null)
            {
                Debug.LogError("[BVUIComponent] UIContainer 未就绪。");
                return;
            }

            GameObject tplTowerList = GetPrefabTemplate(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.TowerList);
            if (tplTowerList == null)
            {
                return;
            }

            this.nodeTowerList = GameObject.Instantiate(tplTowerList);
            this.nodeTowerList.transform.SetParent(this.rootGameObject.transform);
            this.nodeTowerList.transform.position = this.battleView.initTran;
            this.nodeTowerList.transform.GetComponent<Canvas>().sortingOrder = 20;

            GameObject tplBtnTower = GetPrefabTemplate(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.BtnTowerBuild);
            if (tplBtnTower == null)
            {
                return;
            }

            for (int i = 0; i <= this.towerComponent.canBuildTowerListLength - 1; i++)
            {
                GameObject itemGo = GameObject.Instantiate(tplBtnTower);
                UIImageLoader loader = itemGo.GetComponent<UIImageLoader>();
                if (loader != null)
                {
                    GameObject.Destroy(loader);
                }

                this.buttonTowerList[i] = new ButtonTower();
                this.buttonTowerList[i].LoadInfo(this);
                this.buttonTowerList[i].InitInfo(itemGo.transform, this.towerComponent.canBuildTowerList[i]);

                itemGo.transform.SetParent(this.nodeTowerList.transform);
                itemGo.transform.localPosition = Vector3.zero;
                itemGo.transform.localScale = Vector3.one;
            }

            GameObject tplHandleCanvas = GetPrefabTemplate(FightViewPrefabAb.FightViewBundle, FightViewPrefabAb.HandleTowerCanvas);
            if (tplHandleCanvas == null)
            {
                return;
            }

            this.nodeHandleTowerCanvas = GameObject.Instantiate(tplHandleCanvas);
            this.nodeHandleTowerCanvas.transform.SetParent(this.rootGameObject.transform);
            this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
            this.nodeHandleTowerCanvas.transform.GetComponent<Canvas>().sortingOrder = 20;

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.BtnCantUpLevel, out this.spriteButtonUpList[0]))
            {
                Debug.LogError("[BVUIComponent] Btn_CantUpLevel 未预加载");
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.BtnCanUpLevel, out this.spriteButtonUpList[1]))
            {
                Debug.LogError("[BVUIComponent] Btn_CanUpLevel 未预加载");
            }

            if (!FightViewSpriteAb.TryGetNormalMordel(FightViewSpriteAb.BtnReachHighestLevel, out this.spriteButtonUpList[2]))
            {
                Debug.LogError("[BVUIComponent] Btn_ReachHighestLevel 未预加载");
            }

            GameObject tplNodeMap = GetPrefabTemplate(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeMap);
            if (tplNodeMap == null)
            {
                return;
            }

            this.nodeMap = GameObject.Instantiate(tplNodeMap);
            this.nodeMap.transform.SetParent(this.rootGameObject.transform);
            this.nodeMap.transform.position = new Vector3(6, 4.35f, 0);
            if (this.pveDataComponent == null)
            {
                Debug.LogError("[BVUIComponent] 缺少 BattlePVEDataComponent，跳过小地图贴图。");
                return;
            }

            int bigLevel = this.pveDataComponent.bigLevel;
            int level = this.pveDataComponent.level;

            string bgAsset = FightViewSpriteAb.MapBgAssetName(bigLevel, level);
            SpriteRenderer mapBg = this.nodeMap.transform.Find("img_bg").GetComponent<SpriteRenderer>();
            mapBg.SetSprite(FightViewSpriteAb.MapSpriteBundle(bigLevel, bgAsset), bgAsset);
            mapBg.sortingOrder = 0;

            string roadAsset = FightViewSpriteAb.MapRoadAssetName(bigLevel, level);
            SpriteRenderer mapRoad = this.nodeMap.transform.Find("img_road").GetComponent<SpriteRenderer>();
            mapRoad.SetSprite(FightViewSpriteAb.MapSpriteBundle(bigLevel, roadAsset), roadAsset);
            mapRoad.sortingOrder = 1;

            GameObject tplTargetSignal = GetPrefabTemplate(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.NodeTargetSignal);
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
            this.RefreshButtonInfo(0);

            this.AddListener();
            this.uiContentBuilt = true;
            this.IsBuilt = true;
        }

        /// <summary>同关重开：Model 重置完成后刷新绑定与 UI 状态，保留已实例化的预制体。</summary>
        public override void ResetRound(BattleViewResetPass pass)
        {
            if (pass != BattleViewResetPass.AfterModel)
            {
                return;
            }

            this.RefreshBattleBindings();
            this.towerComponent = (BattleTowerComponent)this.battle.GetComponent(BattleComponentType.TowerComponent);
            this.dataComponent = (BattleDataComponent)this.battle.GetComponent(BattleComponentType.DataComponent);
            this.pveDataComponent = BattlePVEDataComponent.GetFrom(this.battle);
            this.mapComponent = (BattleMapComponent)this.battle.GetComponent(BattleComponentType.MapComponent);
            this.pveMapComponent = BattlePVEMapComponent.GetFrom(this.battle);

            if (!this.uiContentBuilt)
            {
                this.Init();
                return;
            }

            this.RemoveListener();
            this.selectGrid = null;
            this.tranTarget = null;

            // 将浮动 UI 移回屏幕外锚点，等价于「收起」而非 Destroy。
            if (this.nodeHandleTowerCanvas != null)
            {
                this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
            }

            if (this.nodeTowerList != null)
            {
                this.nodeTowerList.transform.position = this.battleView.initTran;
            }

            if (this.nodeTargetSignal != null)
            {
                this.nodeTargetSignal.transform.position = this.battleView.initTran;
            }

            this.battleView.bvEventDispatcher.DispatchEvent(BattleEvent.TOWER_RANGE_FADE);
            this.RefreshStartPointPosition();

            if (this.carrot != null)
            {
                this.carrot.Dispose();
                this.carrot.Init(this.battleView.battle);
                this.RefreshCarrotPosition();
            }

            if (this.buttonTowerList != null && this.dataComponent != null)
            {
                for (int i = 0; i < this.buttonTowerList.Length; i++)
                {
                    ButtonTower buttonTower = this.buttonTowerList[i];
                    if (buttonTower != null)
                    {
                        buttonTower.UpdateButtonSprite(this.dataComponent.CoinCount);
                    }
                }
            }

            this.RefreshButtonInfo(0);
            this.AddListener();
        }

        /// <summary>根据路径首尾两点微调出怪点偏移，避免箭头与路径重叠。</summary>
        void RefreshStartPointPosition()
        {
            if (this.nodeMonsterPoint == null || this.pveMapComponent == null ||
                this.pveMapComponent.monsterPathList == null || this.pveMapComponent.monsterPathList.Count < 2)
            {
                return;
            }

            Fix64Vector2 startPosition = this.pveMapComponent.monsterPathList[0];
            bool isRight = this.pveMapComponent.monsterPathList[1].X - this.pveMapComponent.monsterPathList[0].X > Fix64.Zero;
            bool isUp = this.pveMapComponent.monsterPathList[1].Y - this.pveMapComponent.monsterPathList[0].Y > Fix64.Zero;

            if (this.pveMapComponent.monsterPathList[1].X - this.pveMapComponent.monsterPathList[0].X != Fix64.Zero)
            {
                this.nodeMonsterPoint.transform.position = isRight
                    ? new Vector3((float)startPosition.X, (float)startPosition.Y + 0.5f, 0)
                    : new Vector3((float)startPosition.X, (float)startPosition.Y + 0.3f, 0);
            }
            else
            {
                this.nodeMonsterPoint.transform.position = isUp
                    ? new Vector3((float)startPosition.X - 0.1f, (float)startPosition.Y - 0.5f, 0)
                    : new Vector3((float)startPosition.X - 0.1f, (float)startPosition.Y + 0.5f, 0);
            }
        }

        void RefreshCarrotPosition()
        {
            if (this.carrot == null || this.pveMapComponent == null ||
                this.pveMapComponent.monsterPathList == null || this.pveMapComponent.monsterPathList.Count == 0)
            {
                return;
            }

            Fix64Vector2 endPosition = this.pveMapComponent.monsterPathList[this.pveMapComponent.monsterPathList.Count - 1];
            this.carrot.transform.position = new Vector3((float)endPosition.X + 0.1f, (float)endPosition.Y + 0.5f, 0);
        }

        private void SetStartPoint()
        {
            if (this.nodeMonsterPoint == null)
            {
                GameObject tplStart = GetPrefabTemplate(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.StartPoint);
                if (tplStart == null)
                {
                    return;
                }

                this.nodeMonsterPoint = GameObject.Instantiate(tplStart);
                this.nodeMonsterPoint.transform.SetParent(this.rootGameObject.transform);
            }

            this.RefreshStartPointPosition();
        }

        private void SetCarrot()
        {
            if (this.nodeCarrot == null)
            {
                GameObject tplCarrot = GetPrefabTemplate(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.Carrot);
                if (tplCarrot == null)
                {
                    return;
                }

                this.nodeCarrot = GameObject.Instantiate(tplCarrot);
                this.nodeCarrot.transform.SetParent(this.rootGameObject.transform);
                this.carrot = this.nodeCarrot.transform.GetComponent<Carrot>();
                if (this.carrot == null)
                {
                    Debug.LogError("[BVUIComponent] 萝卜预制体缺少 Carrot 组件。");
                    return;
                }
            }

            this.carrot.Init(this.battleView.battle);
            this.RefreshCarrotPosition();
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

        /// <summary>选中已有塔的格子：弹出升级/出售面板并显示攻击范围。</summary>
        private void ShowHandleTowerCanvas(GridPoint grid)
        {
            this.selectGrid = grid;
            this.nodeHandleTowerCanvas.transform.position = new Vector3((float)grid.mapGrid.realX, (float)grid.mapGrid.realY, 0);
            this.battleView.bvEventDispatcher.DispatchEvent<GridPoint>(BattleEvent.TOWER_RANGE_SHOW, grid);
            this.CorrectHandleTowerCanvasGoPosition(grid);
            this.RefreshButtonInfo(0);
        }

        /// <summary>同步建塔按钮与当前选中塔的升级/出售按钮状态（金币、等级上限）。</summary>
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

        /// <summary>地图边缘格子的升级/出售按钮需偏移，防止 UI 超出可视区域。</summary>
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

        /// <summary>收起升级/出售面板并隐藏塔攻击范围。</summary>
        private void FadeHandleTowerCanvas()
        {
            this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
            this.battleView.bvEventDispatcher.DispatchEvent(BattleEvent.TOWER_RANGE_FADE);
        }

        /// <summary>在空格子处弹出建塔列表。</summary>
        private void ShowTowerList(GridPoint grid)
        {
            this.nodeTowerList.transform.position = new Vector3((float)grid.mapGrid.realX, (float)grid.mapGrid.realY, 0);
            this.nodeTowerList.transform.position += this.CorrectTowerListGoPosition(grid);
            this.RefreshButtonInfo(0);
        }

        /// <summary>地图边缘格子的建塔列表需偏移，防止列表超出可视区域。</summary>
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

        /// <summary>收起建塔列表（移回 initTran）。</summary>
        private void FadeTowerList()
        {
            this.nodeTowerList.transform.position = this.battleView.initTran;
        }

        /// <summary>
        /// 统一收起格子展开态：隐藏高亮、建塔列表与升级/出售面板。
        /// 建塔完成、切换道具目标等场景均需调用，避免 UI 叠层残留。
        /// </summary>
        void CollapseGridSelection()
        {
            if (this.selectGrid != null)
            {
                this.selectGrid.HideGrid();
                this.selectGrid = null;
            }

            this.FadeHandleTowerCanvas();
            this.FadeTowerList();
        }

        /// <summary>
        /// 格子点击入口（由 <see cref="GridPoint"/> 经 bvEventDispatcher 派发）。
        /// 可建造格：展开/切换/收起；不可建造格：播放错误反馈并清除旧选中。
        /// </summary>
        public void HandleGrid(GridPoint grid)
        {
            if (grid.mapGrid.state.canBuild)
            {
                if (selectGrid == null)
                {
                    selectGrid = grid;
                    selectGrid.ShowGrid();
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/GridSelect");
                }
                else if (grid == selectGrid)
                {
                    grid.HideGrid();
                    selectGrid = null;
                    this.nodeHandleTowerCanvas.transform.position = this.battleView.initTran;
                    AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Grid/GridDeselect");
                }
                else
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
            if (tower == null)
            {
                Debug.Log("当前格子没有防御塔，无法升级");
                return;
            }

            if (tower.isMaxLevel == true) return;
            InputOrder order = new InputOrder();
            order.SetOrder(this.battle.curFrameId + 1, this.selectGrid.mapGrid.x, this.selectGrid.mapGrid.y, InputOrderType.UPDATE_ORDER);
            ((BattleInputComponent)this.battle.GetComponent(BattleComponentType.InputComponent)).AddOrder(order);
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
            ((BattleInputComponent)this.battle.GetComponent(BattleComponentType.InputComponent)).AddOrder(order);
            this.selectGrid.HideGrid();
        }

        /// <summary>Model 侧塔单位落地后收起格子 UI。</summary>
        private void UpdateNodeState(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.TOWER))
            {
                this.CollapseGridSelection();
            }
        }

        /// <summary>道具被移除时，若其为当前目标则隐藏指示器。</summary>
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

        /// <summary>
        /// 道具目标切换（BattleEvent.TARGET_CHANGE）：先收起格子 UI，再切换/取消目标指示器。
        /// 三次点击同一目标视为取消选中。
        /// </summary>
        private void SetTargetSignal(BattleUnit unit)
        {
            this.CollapseGridSelection();

            if (this.tranTarget == null)
            {
                this.tranTarget = unit;
                this.ShowTargetSignal();
            }
            else if (this.tranTarget != unit)
            {
                this.tranTarget = unit;
                this.ShowTargetSignal();
            }
            else
            {
                this.tranTarget = null;
                this.FadeTargetSignal();
            }
        }

        /// <summary>离战斗场景：销毁全部 UI 节点并重置 uiContentBuilt。</summary>
        public override void ClearGameInfo()
        {
            this.uiContentBuilt = false;
            this.IsBuilt = false;

            if (this.carrot != null)
            {
                this.carrot.Dispose();
                this.carrot = null;
            }

            if (this.buttonTowerList != null)
            {
                for (int i = 0; i < this.buttonTowerList.Length; i++)
                {
                    ButtonTower buttonTower = this.buttonTowerList[i];
                    if (buttonTower != null)
                    {
                        buttonTower.Dispose();
                        this.buttonTowerList[i] = null;
                    }
                }
            }

            this.RemoveListener();
            this.selectGrid = null;
            if (this.nodeHandleTowerCanvas != null)
            {
                GameObject.Destroy(this.nodeHandleTowerCanvas);
                this.nodeHandleTowerCanvas = null;
            }

            if (this.nodeTowerList != null)
            {
                GameObject.Destroy(this.nodeTowerList);
                this.nodeTowerList = null;
            }

            if (this.nodeCarrot != null)
            {
                GameObject.Destroy(this.nodeCarrot);
                this.nodeCarrot = null;
            }

            if (this.nodeMap != null)
            {
                GameObject.Destroy(this.nodeMap);
                this.nodeMap = null;
            }

            if (this.nodeMonsterPoint != null)
            {
                GameObject.Destroy(this.nodeMonsterPoint);
                this.nodeMonsterPoint = null;
            }

            if (this.nodeTargetSignal != null)
            {
                GameObject.Destroy(this.nodeTargetSignal);
                this.nodeTargetSignal = null;
            }

            this.rootGameObject = null;
            this.tranButtonUp = null;
            this.tranButtonSell = null;
            this.imgButtonUp = null;
            this.txtButtonUp = null;
            this.txtButtonSell = null;
            this.tranTarget = null;
        }

        private void ShowTargetSignal()
        {
            AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Tower/ShootSelect");
            UnitTransformComponent tranComponent = (UnitTransformComponent)this.tranTarget.GetComponent(UnitComponentType.TRANSFORM);
            Fix64Vector2 pos = tranComponent.GetLastPosition();
            Vector3 position = new Vector3((float)pos.X, (float)pos.Y, 0);
            this.nodeTargetSignal.transform.position = position + new Vector3(0, BattleConfig.MAP_RATIO / 2, 0);
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
