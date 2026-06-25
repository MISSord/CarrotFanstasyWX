namespace CarrotFantasy
{
    /// <summary>
    /// 经典 / 生存 / 肉鸽 PVE 公共组件注册与 Init 顺序。
    /// 单逻辑帧 combat 流程（索敌、子弹、碰撞、空间网格）见 <c>Model_Battle/BattleCombatFlow.md</c>。
    /// </summary>
    public static class PveBattleComponentSetup
    {
        public enum Layout
        {
            Classic,
            FlowFieldSurvival,
        }

        /// <summary>
        /// 注册顺序即逻辑帧 <see cref="BaseBattle.SimulateOneLogicFrame"/> 的 OnTick 顺序：
        /// Input → Monster(移动) → Tower(集火+开火) → Bullet(移动) → HitTest(RefreshSpatialGrid+碰撞)。
        /// </summary>
        public static void Register(BaseBattle battle, Layout layout)
        {
            battle.stateMachine = new PveStateMachine(battle);
            battle.AddComponent(new BattlePVEDataComponent(battle));
            battle.AddComponent(new BattleGlobalBuffComponent(battle));
            battle.AddComponent(new BattlePVEMapComponent(battle));
            battle.AddComponent(new BattleItemComponent(battle));
            battle.AddComponent(new BattleInputComponent(battle));

            if (layout == Layout.FlowFieldSurvival)
            {
                battle.AddComponent(new BattleFlowFieldComponent(battle));
                battle.AddComponent(new BattleSurvivalPVEMonsterComponent(battle));
            }
            else
            {
                battle.AddComponent(new BattlePVEMonsterComponent(battle));
            }

            battle.AddComponent(new BattleTowerComponent(battle));
            battle.AddComponent(new BattleBulletComponent(battle));
            battle.AddComponent(new BattleSimpleHitTestComponent(battle));
            battle.AddComponent(new BattleSchedulerComponent(battle));
        }

        /// <summary>Map 先于 HitTest Init（空间网格边界）；HitTest 先于 ItemComponent Init（物品 BATTLE_UNIT_ADD 须有人监听）。</summary>
        public static void InitAll(BaseBattle battle, Layout layout)
        {
            battle.GetComponent(BattleComponentType.DataComponent).Init();
            battle.GetComponent(BattleComponentType.GlobalBuffComponent).Init();
            battle.GetComponent(BattleComponentType.MapComponent).Init();
            battle.GetComponent(BattleComponentType.InputComponent).Init();

            if (layout == Layout.FlowFieldSurvival)
            {
                battle.GetComponent(BattleComponentType.FlowFieldComponent).Init();
            }

            battle.GetComponent(BattleComponentType.MonsterComponent).Init();
            battle.GetComponent(BattleComponentType.TowerComponent).Init();
            battle.GetComponent(BattleComponentType.BulletComponent).Init();
            battle.GetComponent(BattleComponentType.HitTestComponent).Init();
            battle.GetComponent(BattleComponentType.ItemComponent).Init();
            battle.GetComponent(BattleComponentType.SchedulerComponent).Init();
        }
    }
}
