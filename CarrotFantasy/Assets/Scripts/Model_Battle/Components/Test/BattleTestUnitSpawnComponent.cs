using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 碰撞/性能测试刷怪刷弹：随机格子出生，直线走向另一随机格子，到达后自我消亡。
    /// </summary>
    public class BattleTestUnitSpawnComponent : BaseBattleComponent
    {
        public const string ComponentTypeId = "TestUnitSpawnComponent";

        private readonly List<BattleUnit_Monster_Test> testMonsters = new List<BattleUnit_Monster_Test>();
        private readonly List<BattleUnit_Bullet_Test> testBullets = new List<BattleUnit_Bullet_Test>();
        private readonly List<BattleUnit_Monster_Test> deadMonsters = new List<BattleUnit_Monster_Test>();
        private readonly List<BattleUnit_Bullet_Test> deadBullets = new List<BattleUnit_Bullet_Test>();

        private BattleTestMapComponent mapComponent;
        private BattleTestDataComponent dataComponent;
        private MonsterConfigReader monsterConfigReader;
        private BulletConfigReader bulletConfigReader;
        private BattleTestRandom rng;

        private int spawnIntervalFrames = 15;
        private int monstersPerBatch = 4;
        private int bulletsPerBatch = 8;
        private int framesSinceSpawn;

        public BattleTestUnitSpawnComponent(BaseBattle bBattle) : base(bBattle)
        {
            this.componentType = ComponentTypeId;
        }

        public int AliveTestUnitCount
        {
            get { return this.testMonsters.Count + this.testBullets.Count; }
        }

        public override void Init()
        {
            this.mapComponent = this.baseBattle.GetComponent(BattleComponentType.MapComponent) as BattleTestMapComponent;
            this.dataComponent = this.baseBattle.GetComponent(BattleComponentType.DataComponent) as BattleTestDataComponent;
            this.monsterConfigReader = MonsterConfigReader.Instance;
            this.bulletConfigReader = BulletConfigReader.Instance;

            uint seed = (uint)(this.dataComponent.monsterConfigBigLevel * 1000 + 17 + 0xBEEF);
            this.rng = new BattleTestRandom(seed);

            if (BattleParamServer.Instance != null)
            {
                if (BattleParamServer.Instance.testUnitsSpawnIntervalFrames > 0)
                {
                    this.spawnIntervalFrames = BattleParamServer.Instance.testUnitsSpawnIntervalFrames;
                }

                if (BattleParamServer.Instance.testMonstersPerBatch > 0)
                {
                    this.monstersPerBatch = BattleParamServer.Instance.testMonstersPerBatch;
                }

                if (BattleParamServer.Instance.testBulletsPerBatch > 0)
                {
                    this.bulletsPerBatch = BattleParamServer.Instance.testBulletsPerBatch;
                }
            }

            this.eventDispatcher.AddListener(BattleEvent.START_GAME, this.OnStartGame);
        }

        private void OnStartGame()
        {
            this.framesSinceSpawn = this.spawnIntervalFrames;
        }

        public override void OnTick(Fix64 time)
        {
            if (!this.baseBattle.isStart)
            {
                return;
            }

            this.framesSinceSpawn += 1;
            if (this.framesSinceSpawn >= this.spawnIntervalFrames)
            {
                this.framesSinceSpawn = 0;
                this.SpawnBatch();
            }

            for (int i = 0; i < this.testMonsters.Count; i++)
            {
                this.testMonsters[i].OnTick(time);
            }

            for (int i = 0; i < this.testBullets.Count; i++)
            {
                this.testBullets[i].OnTick(time);
            }

            this.FlushDeadUnits();
        }

        public override void LateTick(Fix64 time)
        {
            for (int i = 0; i < this.testMonsters.Count; i++)
            {
                this.testMonsters[i].LateTick(time);
            }

            for (int i = 0; i < this.testBullets.Count; i++)
            {
                this.testBullets[i].LateTick(time);
            }
        }

        private void SpawnBatch()
        {
            for (int i = 0; i < this.monstersPerBatch; i++)
            {
                this.SpawnOneTestMonster();
            }

            for (int i = 0; i < this.bulletsPerBatch; i++)
            {
                this.SpawnOneTestBullet();
            }
        }

        private void SpawnOneTestMonster()
        {
            int startGx;
            int startGy;
            int targetGx;
            int targetGy;
            BattleTestGridCellPicker.PickTwoDistinctCells(this.mapComponent, this.rng, out startGx, out startGy, out targetGx, out targetGy);

            Fix64Vector2 birth = BattleTestGridCellPicker.CellToWorld(this.mapComponent, startGx, startGy);
            Fix64Vector2 target = BattleTestGridCellPicker.CellToWorld(this.mapComponent, targetGx, targetGy);

            int visualMonsterId = this.rng.NextInt(1, 13);
            if (!this.monsterConfigReader.monsterBirthParam.ContainsKey(visualMonsterId))
            {
                visualMonsterId = 1;
            }

            Dictionary<string, Fix64> param = this.monsterConfigReader.GetSingleMonsterConfig(visualMonsterId);

            BattleUnit_Monster_Test monster = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Monster_Test>(BattleUnitType.MONSTER);
            if (monster == null)
            {
                monster = new BattleUnit_Monster_Test(this.baseBattle);
            }

            monster.eventDipatcher.AddListener<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this.OnTestMonsterDied);
            monster.LoadInfo(this.baseBattle.GetUid(), param, birth);
            monster.LoadInfo2(this.dataComponent.monsterConfigBigLevel, visualMonsterId);
            monster.Init();
            monster.LoadGridMoveTarget(target);
            monster.InitComponents();

            this.testMonsters.Add(monster);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.MONSTER, monster);
        }

        private void SpawnOneTestBullet()
        {
            int startGx;
            int startGy;
            int targetGx;
            int targetGy;
            BattleTestGridCellPicker.PickTwoDistinctCells(this.mapComponent, this.rng, out startGx, out startGy, out targetGx, out targetGy);

            Fix64Vector2 birth = BattleTestGridCellPicker.CellToWorld(this.mapComponent, startGx, startGy);
            Fix64Vector2 target = BattleTestGridCellPicker.CellToWorld(this.mapComponent, targetGx, targetGy);

            int bulletConfigId = 101;
            Dictionary<string, Fix64> param = this.bulletConfigReader.GetSingleBulletConfig(bulletConfigId);

            BattleUnit_Bullet_Test bullet = BattleUnitPool.Instance.GetNewBattleUnit<BattleUnit_Bullet_Test>(BattleUnitType.BULLET);
            if (bullet == null)
            {
                bullet = new BattleUnit_Bullet_Test(this.baseBattle);
            }

            bullet.eventDipatcher.AddListener<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this.OnTestBulletRemoved);
            bullet.LoadInfo(this.baseBattle.GetUid(), param, birth);
            bullet.LoadInfo2(null, null);
            bullet.Init();
            bullet.LoadGridMoveTarget(target);
            bullet.InitComponents();

            this.testBullets.Add(bullet);
            this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_ADD, BattleUnitType.BULLET, bullet);
        }

        private void OnTestMonsterDied(BattleUnit_Monster monster)
        {
            BattleUnit_Monster_Test test = monster as BattleUnit_Monster_Test;
            if (test != null && !this.deadMonsters.Contains(test))
            {
                test.MarkDiedAtTarget();
                this.deadMonsters.Add(test);
            }
        }

        private void OnTestBulletRemoved(BattleUnit_Bullet bullet)
        {
            BattleUnit_Bullet_Test test = bullet as BattleUnit_Bullet_Test;
            if (test != null && !this.deadBullets.Contains(test))
            {
                this.deadBullets.Add(test);
            }
        }

        private void FlushDeadUnits()
        {
            for (int i = 0; i < this.testMonsters.Count; i++)
            {
                BattleUnit_Monster_Test m = this.testMonsters[i];
                if (((BattleUnit_Monster_Test)m).IsDead() && !this.deadMonsters.Contains(m))
                {
                    m.MarkDiedAtTarget();
                    this.deadMonsters.Add(m);
                }
            }

            for (int i = 0; i < this.testBullets.Count; i++)
            {
                BattleUnit_Bullet_Test b = this.testBullets[i];
                if (b.IsFinished() && !this.deadBullets.Contains(b))
                {
                    this.deadBullets.Add(b);
                }
            }

            for (int i = 0; i < this.deadMonsters.Count; i++)
            {
                BattleUnit_Monster_Test m = this.deadMonsters[i];
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.MONSTER, m);
                m.eventDipatcher.RemoveListener<BattleUnit_Monster>(BattleEvent.MONSTER_DIED, this.OnTestMonsterDied);
                m.ClearInfo();
                this.testMonsters.Remove(m);
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.MONSTER, m);
            }

            this.deadMonsters.Clear();

            for (int i = 0; i < this.deadBullets.Count; i++)
            {
                BattleUnit_Bullet_Test b = this.deadBullets[i];
                this.eventDispatcher.DispatchEvent<String, BattleUnit>(BattleEvent.BATTLE_UNIT_REMOVE, BattleUnitType.BULLET, b);
                b.eventDipatcher.RemoveListener<BattleUnit_Bullet>(BattleEvent.BULLET_REMOVE, this.OnTestBulletRemoved);
                b.ClearInfo();
                this.testBullets.Remove(b);
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.BULLET, b);
            }

            this.deadBullets.Clear();
        }

        public override void ClearInfo()
        {
            this.eventDispatcher.RemoveListener(BattleEvent.START_GAME, this.OnStartGame);

            for (int i = this.testMonsters.Count - 1; i >= 0; i--)
            {
                this.testMonsters[i].ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.MONSTER, this.testMonsters[i]);
            }

            for (int i = this.testBullets.Count - 1; i >= 0; i--)
            {
                this.testBullets[i].ClearInfo();
                BattleUnitPool.Instance.PushObjectToPool(BattleUnitType.BULLET, this.testBullets[i]);
            }

            this.testMonsters.Clear();
            this.testBullets.Clear();
            this.deadMonsters.Clear();
            this.deadBullets.Clear();
            this.framesSinceSpawn = 0;
        }
    }
}
