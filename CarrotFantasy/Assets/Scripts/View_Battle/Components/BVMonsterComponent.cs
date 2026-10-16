using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public class BVMonsterComponent : BaseBattleViewComponent
    {
        private GameObject rootGameObject;
        private int _useEpoch;

        private Dictionary<BattleUnit_Monster, BattleUnitView_Monster> monsterDic = new Dictionary<BattleUnit_Monster, BattleUnitView_Monster>();

        public BVMonsterComponent(BattleView_base battleView) : base(battleView)
        {
            this.componentType = BattleViewComponentType.MONSTER;
        }

        public override void CollectWarm(AssetBundleBatch batch)
        {
            batch.Keep<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterPrefab);
            batch.Keep<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterCanvas);
        }

        public override void Init()
        {
            BVSceneComponent scene = (BVSceneComponent)this.battleView.GetComponent(BattleViewComponentType.SCENE);
            this.rootGameObject = scene.RegisterGameContainer("MonsterContainer");

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
                    GameObject monsterTpl = this.BundleScope.Get<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterPrefab);
                    if (monsterTpl == null)
                    {
                        return;
                    }

                    node = GameObject.Instantiate(monsterTpl);
                    node.transform.SetParent(this.rootGameObject.transform);
                }

                GameObject canvasTpl = this.BundleScope.Get<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.MonsterCanvas);
                GameObject hpCanvasGo = canvasTpl != null ? GameObject.Instantiate(canvasTpl) : null;
                monsterView.InitTransform(node.transform);
                monsterView.AttachMonsterHpCanvas(hpCanvasGo);
                monsterView.LoadInfo(this.battleView, monster);
                monsterView.Init();

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

            monsterView.ClearUnitInfo();
            GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, monsterView.transform.gameObject);
            this.monsterDic.Remove(monster);
            GameViewObjectPool.Instance.PushViewObjectToPool(BattleUnitViewType.Monster, monsterView);
            AudioManager.Instance.PlayEffectByResources(String.Format("AudioClips/NormalMordel/Monster/{0}/{1}", monster.curLevel, monster.monsterId));

            //特效
            GameObject sell = GameViewObjectPool.Instance.GetNewGameObject(BattleUnitViewType.DestroyEffect);
            int epoch = this._useEpoch;
            if (sell == null)
            {
                this.BundleScope.Use<GameObject>(FightViewPrefabAb.FightPartBundle, FightViewPrefabAb.DestoryEffect, tpl =>
                {
                    if (epoch != this._useEpoch || tpl == null)
                    {
                        return;
                    }

                    this.PlayDestroyEffect(GameObject.Instantiate(tpl), unit);
                });
            }
            else
            {
                this.PlayDestroyEffect(sell, unit);
            }
        }

        private void PlayDestroyEffect(GameObject sell, BattleUnit unit)
        {
            if (sell == null)
            {
                return;
            }

            sell.transform.GetComponent<Animator>().enabled = true;
            UnitTransformComponent tran = (UnitTransformComponent)unit.GetComponent(UnitComponentType.TRANSFORM);
            sell.transform.position = new Vector3((float)tran.lastFrameX, (float)tran.lastFrameY, 0);
            Sche.DelayExeOnceTimes(() =>
            {
                if (sell == null)
                {
                    return;
                }

                sell.transform.GetComponent<Animator>().enabled = false;
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.DestroyEffect, sell);
            }, 0.5f);
        }

        public override void ClearGameInfo()
        {
            this._useEpoch++;
            foreach (KeyValuePair<BattleUnit_Monster, BattleUnitView_Monster> info in this.monsterDic)
            {
                info.Value.ClearUnitInfo();
                GameViewObjectPool.Instance.PushGameObjectToPool(BattleUnitViewType.Monster, info.Value.transform.gameObject);
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
