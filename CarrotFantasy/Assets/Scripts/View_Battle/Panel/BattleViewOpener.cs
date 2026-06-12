using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>打开战斗内 BaseView 前统一注入 battle。</summary>
    public static class BattleViewOpener
    {
        public static bool Open<T>(BaseBattle battle) where T : BattleBoundView
        {
            if (battle == null)
            {
                Debug.LogError("[BattleViewOpener] Open 失败：battle 为空。");
                return false;
            }

            if (ViewManager.Instance == null)
            {
                Debug.LogError("[BattleViewOpener] Open 失败：ViewManager 未初始化。");
                return false;
            }

            if (!ViewManager.Instance.viewTypeDic.TryGetValue(typeof(T), out BaseView view))
            {
                Debug.LogError("[BattleViewOpener] Open 失败：未注册 " + typeof(T).Name);
                return false;
            }

            T panel = (T)view;
            if (!panel.BindBattle(battle))
            {
                return false;
            }

            ViewManager.Instance.OpenView<T>();
            return true;
        }
    }
}
