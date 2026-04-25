using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVMonsterComponent : BaseBattleViewComponent
    {
        private String pathUrl;
        private GameObject noInstanGameObject;
        private GameObject rootGameObject;

        private BattleSchedulerComponent scheComponent;

        private Dictionary<BattleUnit_Monster, BattleUnitView_Monster> monsterDic = new Dictionary<BattleUnit_Monster, BattleUnitView_Monster>();

        public BVMonsterComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.MONSTER;
            this.pathUrl = "Prefabs/Game/MonsterPrefab";
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("MonsterContainer");
            this.noInstanGameObject = ResourceLoader.Instance.GetGameObject(this.pathUrl);

            this.scheComponent = (BattleSchedulerComponent)this.battle.GetComponent(BattleComponentType.SchedulerComponent);

            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.Monster);
            GameViewObjectPool.Instance.RegisterGameObject(BattleUnitViewType.DestroyEffect);
            this.AddListener();
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
                BattleUnitView_Monster monsterView = GameViewObjectPool.Instance.getNewBattleUnitView<BattleUnitView_Monster>(BattleUnitViewType.Monster);
                GameObject node = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.Monster);
                if (monsterView == null)
                {
                    monsterView = new BattleUnitView_Monster();
                }
                if (node == null)
                {
                    node = GameObject.Instantiate(this.noInstanGameObject);
                    node.transform.SetParent(this.rootGameObject.transform);
                }
                monsterView.InitTransform(node.transform);
                monsterView.LoadInfo(this.battleView, monster);
                monsterView.Init();

                this.monsterDic.Add(monster, monsterView);
                UIServer.Instance.audioManager.PlayEffect("AudioClips/NormalMordel/Monster/Create");
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
            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, monsterView.transform.gameObject);
            monsterView.ClearUnitInfo();
            this.monsterDic.Remove(monster);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Monster, monsterView);
            UIServer.Instance.audioManager.PlayEffect(String.Format("AudioClips/NormalMordel/Monster/{0}/{1}", monster.curLevel, monster.monsterId));

            //特效
            GameObject sell = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DestroyEffect);
            if (sell == null)
            {
                sell = GameObject.Instantiate(ResourceLoader.Instance.GetGameObject("Prefabs/Game/DestoryEffect"));
            }
            sell.transform.GetComponent<Animator>().enabled = true;
            UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
            Sche.DelayExeOnceTimes(() =>
            {
                sell.transform.GetComponent<Animator>().enabled = false;
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, sell);
            }, 0.5f);
        }

        public override void ClearGameInfo()
        {
            foreach (KeyValuePair<BattleUnit_Monster, BattleUnitView_Monster> info in this.monsterDic)
            {
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, info.Value.transform.gameObject);
                info.Value.ClearUnitInfo();
                GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Monster, info.Value);
            }
            this.monsterDic.Clear();
            this.RemoveListener();
        }

        public override void Dispose()
        {
            this.ClearGameInfo();
            base.Dispose();
        }
    }
}
