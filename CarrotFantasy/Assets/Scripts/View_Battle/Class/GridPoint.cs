using DG.Tweening;
using UnityEngine;

namespace CarrotFantasy
{
    public class GridPoint : MonoBehaviour
    {
        //属性
        private SpriteRenderer spriteRenderer;

        private BattleView_base battleView;

        private string itemPrefabUrl;

        public BattleMapGrid mapGrid { get; private set; }

        private Sprite startSprite;
        private Sprite normalSprite;
        private Sprite cantBuildSprite;

        private BVMapComponent bvMapComponent;
        private GameObject levelUpSignalGo;//是否可升级信号



        bool IsSpriteRendererAlive()
        {
            return this.spriteRenderer != null;
        }

        public void InitTrans(BattleView_base battleView)
        {
            this.battleView = battleView;
            this.spriteRenderer = transform.GetComponent<SpriteRenderer>();
            if (!this.IsSpriteRendererAlive())
            {
                Debug.LogWarning("[GridPoint] 缺少 SpriteRenderer: " + name, this);
                return;
            }

            Transform levelUpSignal = transform.Find("LevelUpSignal");
            if (levelUpSignal == null)
            {
                Debug.LogWarning("[GridPoint] 缺少 LevelUpSignal: " + name, this);
                return;
            }

            levelUpSignalGo = levelUpSignal.gameObject;
            levelUpSignalGo.SetActive(false);
        }

        public void InitInfo(int x, int y)
        {
            BVMapComponent bvMap = (BVMapComponent)battleView.GetComponent(BattleViewComponentType.MAP);
            this.startSprite = bvMap.sprGirdStartState;
            this.normalSprite = bvMap.sprGirdNoramlState;
            this.cantBuildSprite = bvMap.sprGirdCantBuildState;

            BattleMapComponent map = (BattleMapComponent)(this.battleView.battle.GetComponent(BattleComponentType.MapComponent));
            this.mapGrid = map.gridsList[x, y];
            this.UpdateGrid();
        }

        /// <summary>同关重开：停止 tween、隐藏格子与升级提示，不销毁节点。</summary>
        public void ResetRound()
        {
            if (this.spriteRenderer != null)
            {
                this.spriteRenderer.DOKill();
                this.spriteRenderer.enabled = false;
                this.spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            }

            this.transform.DOKill();

            if (this.levelUpSignalGo != null)
            {
                this.levelUpSignalGo.SetActive(false);
            }
        }

        public void StartGame()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            this.spriteRenderer.sprite = this.startSprite;
            this.spriteRenderer.DOKill();
            this.spriteRenderer
                .DOColor(new Color(1, 1, 1, 0.2f), 3f)
                .OnComplete(ChangeSpriteToGrid);
        }

        //改回原来样式的Sprite
        private void ChangeSpriteToGrid()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            spriteRenderer.color = new Color(1, 1, 1, 1);

            if (this.mapGrid.state.canBuild)
            {
                spriteRenderer.sprite = this.normalSprite;
            }
            else
            {
                spriteRenderer.sprite = this.cantBuildSprite;
            }

            spriteRenderer.enabled = false;
        }


        //更新格子状态（默认隐藏，选中时由 ShowGrid 显示）
        public void UpdateGrid()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            spriteRenderer.enabled = false;
        }

        /// <summary>
        /// 有关格子处理的方法
        /// </summary>

        private void OnDestroy()
        {
            if (this.spriteRenderer != null)
            {
                this.spriteRenderer.DOKill();
            }

            this.transform.DOKill();
        }

        private void OnMouseDown()
        {
            //选择的是UI则不发生交互
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            this.battleView.bvEventDispatcher.DispatchEvent<GridPoint>(BattleViewEventType.Select_Grid, this);
        }

        public void ShowGrid()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            if (this.mapGrid.hasTower == true)
            {
                spriteRenderer.enabled = true;
                this.battleView.bvEventDispatcher.DispatchEvent<GridPoint>(BattleViewEventType.Show_Handle_Tower, this);
            }
            else
            {
                //handleTowerGanvasGo.SetActive(true);
                //显示塔的攻击范围
                //towerGo.transform.Find("attackRange").gameObject.SetActive(true);

                spriteRenderer.enabled = true;
                //展示建塔列表
                this.battleView.bvEventDispatcher.DispatchEvent<GridPoint>(BattleViewEventType.Show_Tower_List, this);
            }
        }

        public void HideGrid()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            if (this.mapGrid.hasTower == true)
            {
                //隐藏建塔列表
                this.battleView.bvEventDispatcher.DispatchEvent(BattleViewEventType.Fade_Handle_Tower);
            }
            else
            {
                //handleTowerGanvasGo.SetActive(false);
                //隐藏塔的范围
                //towerGo.transform.Find("attackRange").gameObject.SetActive(false);
                //隐藏建塔列表
                this.battleView.bvEventDispatcher.DispatchEvent(BattleViewEventType.Fade_Tower_List);
            }
            spriteRenderer.enabled = false;
        }

        public void SetLevelUpSignalVisible(bool visible)
        {
            if (this.levelUpSignalGo == null)
            {
                return;
            }

            this.levelUpSignalGo.SetActive(visible);
        }

        //显示此格子不能够去建塔
        public void ShowCantBuild()
        {
            if (!this.IsSpriteRendererAlive())
            {
                return;
            }

            spriteRenderer.enabled = true;
            spriteRenderer.DOKill();
            spriteRenderer
                .DOColor(new Color(1, 1, 1, 0), 2f)
                .OnComplete(() =>
                {
                    if (!this.IsSpriteRendererAlive())
                    {
                        return;
                    }

                    spriteRenderer.enabled = false;
                    spriteRenderer.color = new Color(1, 1, 1, 1);
                });
        }
    }
}

