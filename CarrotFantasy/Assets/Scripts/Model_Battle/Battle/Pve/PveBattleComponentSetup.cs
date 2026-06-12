namespace CarrotFantasy
{
    /// <summary>经典 / 生存 / 肉鸽 PVE 公共组件注册与 Init 顺序。</summary>
    public static class PveBattleComponentSetup
    {
        public enum Layout
        {
            Classic,
            FlowFieldSurvival,
        }

        public static void Register(BaseBattle battle, Layout layout)
        {
            battle.stateMachine = new PveStateMachine(battle);
            battle.AddComponent(new BattlePVEDataComponent(battle));
            battle.AddComponent(new BattleGlobalBuffComponent(battle));
            battle.AddComponent(new BattleSimpleHitTestComponent(battle));
            battle.AddComponent(new BattlePVEMapComponent(battle));
            battle.AddComponent(new BattleItemComponent(battle));
            battle.AddComponent(new BattleTowerComponent(battle));

            if (layout == Layout.FlowFieldSurvival)
            {
                battle.AddComponent(new BattleFlowFieldComponent(battle));
                battle.AddComponent(new BattleSurvivalPVEMonsterComponent(battle));
            }
            else
            {
                battle.AddComponent(new BattlePVEMonsterComponent(battle));
            }

            battle.AddComponent(new BattleBulletComponent(battle));
            battle.AddComponent(new BattleInputComponent(battle));
            battle.AddComponent(new BattleSchedulerComponent(battle));
        }

        public static void InitAll(BaseBattle battle, Layout layout)
        {
            battle.GetComponent(BattleComponentType.DataComponent).Init();
            battle.GetComponent(BattleComponentType.GlobalBuffComponent).Init();
            battle.GetComponent(BattleComponentType.HitTestComponent).Init();
            battle.GetComponent(BattleComponentType.MapComponent).Init();
            battle.GetComponent(BattleComponentType.ItemComponent).Init();
            battle.GetComponent(BattleComponentType.TowerComponent).Init();

            if (layout == Layout.FlowFieldSurvival)
            {
                battle.GetComponent(BattleComponentType.FlowFieldComponent).Init();
            }

            battle.GetComponent(BattleComponentType.MonsterComponent).Init();
            battle.GetComponent(BattleComponentType.BulletComponent).Init();
            battle.GetComponent(BattleComponentType.InputComponent).Init();
            battle.GetComponent(BattleComponentType.SchedulerComponent).Init();
        }
    }
}
