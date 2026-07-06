using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVMonsterComponent : BaseBattleViewComponent
    {
        private GameObject noInstanGameObject;
        private GameObject _monsterCanvasTemplate;
        private GameObject rootGameObject;

        private BattleSchedulerComponent scheComponent;

        private Dictionary<BattleUnit_Monster, BattleUnitView_Monster> monsterDic = new Dictionary<BattleUnit_Monster, BattleUnitView_Monster>();

        public BVMonsterComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.MONSTER;
        }

        public override void Init()
        {
            BVSceneComponent scene = this.battleView.TryGetComponent(BattleViewComponentType.SCENE) as BVSceneComponent;
            if (scene == null)
            {
                Debug.LogError("[BVMonsterComponent] BVSceneComponent 未注册。");
                return;
            }

            this.rootGameObject = scene.RegisterGameContainer("MonsterContainer");
            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.MonsterPrefab,
                out this.noInstanGameObject))
            {
                Debug.LogError("[BVMonsterComponent] MonsterPrefab 未预加载");
            }

            if (!BattleViewPrefabPreloader.TryGetTemplate(
                FightViewPrefabAb.FightPartBundle,
                FightViewPrefabAb.MonsterCanvas,
                out this._monsterCanvasTemplate))
            {
                Debug.LogError("[BVMonsterComponent] MonsterCanvas 未预加载");
            }

            this.scheComponent = (BattleSchedulerComponent)this.battle.GetComponent(BattleComponentType.SchedulerComponent);

            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.Monster);
            BattleViewEffectHelper.EnsureDestroyEffectPoolRegistered();
            this.RemoveListener();
            this.AddListener();
            this.IsBuilt = true;
        }

        private void AddListener()
        {
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewMonsterView);
            this.eventDispatcher.AddListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveMonsterView);
        }

        private void RemoveListener()
        {
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, this.RegisterNewMonsterView);
            this.eventDispatcher.RemoveListener<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, this.RemoveMonsterView);
        }

        private void RegisterNewMonsterView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.MONSTER))
            {
                BattleUnit_Monster monster = (BattleUnit_Monster)unit;

                BattleUnitView_Monster existingView;
                if (this.monsterDic.TryGetValue(monster, out existingView))
                {
                    Debug.LogWarning("[BVMonsterComponent] 怪物视图已存在，跳过重复注册: uid=" + monster.uid);
                    return;
                }

                BattleUnitView_Monster monsterView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Monster>(BattleUnitViewType.Monster);
                GameObject node = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.Monster);
                if (monsterView == null)
                {
                    monsterView = new BattleUnitView_Monster();
                }
                if (node == null)
                {
                    node = GameObject.Instantiate(this.noInstanGameObject);
                }

                BattleView_base.AttachPooledVisualToContainer(node.transform, this.rootGameObject.transform);

                GameObject monsterCanvasTemplate = this._monsterCanvasTemplate;
                if (monsterCanvasTemplate == null)
                {
                    BattleViewPrefabPreloader.TryGetTemplate(
                        FightViewPrefabAb.FightPartBundle,
                        FightViewPrefabAb.MonsterCanvas,
                        out monsterCanvasTemplate);
                    this._monsterCanvasTemplate = monsterCanvasTemplate;
                }

                BVBattleWorldUiComponent worldUi =
                    this.battleView.TryGetComponent(BattleViewComponentType.WORLD_UI) as BVBattleWorldUiComponent;
                if (worldUi == null)
                {
                    Debug.LogError("[BVMonsterComponent] WORLD_UI 组件未注册，无法创建怪物血条。");
                }

                GameObject hpBarGo = worldUi != null
                    ? worldUi.CreateMonsterHpBar(monsterCanvasTemplate)
                    : null;
                if (hpBarGo == null)
                {
                    Debug.LogError(
                        "[BVMonsterComponent] 创建怪物血条失败: templateReady=" + (monsterCanvasTemplate != null) +
                        ", worldUiReady=" + (worldUi != null));
                }
                monsterView.InitTransform(node.transform);
                monsterView.AttachMonsterHpBar(hpBarGo);
                monsterView.LoadInfo(this.battleView, monster);
                monsterView.Init();
                monsterView.ReloadInfo();

                this.monsterDic.Add(monster, monsterView);
                AudioManager.Instance.PlayEffectByResources("AudioClips/NormalMordel/Monster/Create");
            }
        }

        public override void OnTick(float time)
        {
            foreach (KeyValuePair<BattleUnit_Monster, BattleUnitView_Monster> info in this.monsterDic)
            {
                info.Value.OnTick(time);
            }
        }

        private void RemoveMonsterView(String type, BattleUnit unit)
        {
            if (type.Equals(BattleUnitType.MONSTER) == false) return;
            BattleUnit_Monster monster = (BattleUnit_Monster)unit;
            BattleUnitView_Monster monsterView;
            if (!this.monsterDic.TryGetValue(monster, out monsterView))
            {
                Debug.Log("移除怪兽视图出错");
                return;
            }

            Transform monsterTransform = monsterView.transform;
            GameObject monsterGo = monsterTransform != null ? monsterTransform.gameObject : null;
            this.monsterDic.Remove(monster);
            monsterView.ClearUnitInfo();

            if (monsterGo != null)
            {
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, monsterGo);
            }

            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Monster, monsterView);
            AudioManager.Instance.PlayEffectByResources(String.Format("AudioClips/NormalMordel/Monster/{0}/{1}", monster.curLevel, monster.monsterId));
            BattleViewEffectHelper.PlayDestroyAt(unit);
        }

        public override void ReturnUnitsToPoolForReplay()
        {
            this.RemoveListener();
            this.ReturnAllMonstersToPool();
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.Monster);
            GameViewObjectPool.Instance.RegisterBattleUnitView(BattleUnitViewType.Monster);
            BattleViewEffectHelper.EnsureDestroyEffectPoolRegistered();
        }

        public override void ApplyModelForReplay()
        {
            this.RebindBattleListeners(this.RemoveListener, this.AddListener);
        }

        void ReturnAllMonstersToPool()
        {
            foreach (KeyValuePair<BattleUnit_Monster, BattleUnitView_Monster> info in this.monsterDic)
            {
                BattleUnitView_Monster monsterView = info.Value;
                if (monsterView == null)
                {
                    continue;
                }

                Transform monsterTransform = monsterView.transform;
                GameObject monsterGo = monsterTransform != null ? monsterTransform.gameObject : null;
                monsterView.ClearUnitInfo();

                if (monsterGo != null)
                {
                    GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, monsterGo);
                }

                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Monster, monsterView);
            }

            this.monsterDic.Clear();
        }

        public override void ClearGameInfo()
        {
            this._monsterCanvasTemplate = null;
            this.noInstanGameObject = null;
            this.ReturnAllMonstersToPool();
            this.RemoveListener();
            this.IsBuilt = false;
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
