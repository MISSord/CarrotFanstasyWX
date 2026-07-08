using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>打开战斗内 BaseView 前统一注入 battle。</summary>
    public static class BattleViewOpener
    {
        static readonly Type[] BattleViewTypes =
        {
            typeof(NormalModelPanel),
            typeof(MenuView),
            typeof(GameWinView),
            typeof(GameOverView),
        };

        /// <param name="onReady">主战斗面板（NormalModelPanel）就绪后回调；UI 异步加载时延后触发。</param>
        public static bool Open<T>(BaseBattle battle, Action onReady = null) where T : BattleBoundView
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

            panel.SetPendingBattleOpenCallback(onReady);
            ViewManager.Instance.OpenView<T>();
            panel.TryCompleteBattleOpen();
            return true;
        }

        /// <summary>离关时立即释放战斗 UI 缓存，避免 UINameTable / 监听跨局残留。</summary>
        public static void ForceReleaseAllBattleViews()
        {
            if (ViewManager.Instance == null)
            {
                return;
            }

            for (int i = 0; i < BattleViewTypes.Length; i++)
            {
                Type viewType = BattleViewTypes[i];
                if (!ViewManager.Instance.viewTypeDic.TryGetValue(viewType, out BaseView view))
                {
                    continue;
                }

                view.CloseAndReleaseNow();
            }
        }
    }
}
