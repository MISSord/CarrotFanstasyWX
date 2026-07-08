using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>战斗内 BaseView 的打开与释放；离关释放仅由 <see cref="BattleSession.Shutdown"/> 调用。</summary>
    public static class BattleViewOpener
    {
        static readonly Type[] AllBattleViewTypes =
        {
            typeof(NormalModelPanel),
            typeof(MenuView),
            typeof(GameWinView),
            typeof(GameOverView),
        };

        static readonly Type[] OverlayBattleViewTypes =
        {
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

        /// <summary>同关重开：关闭菜单/结算等叠层，保留 NormalModelPanel。</summary>
        public static void CloseOverlayBattleViews()
        {
            CloseBattleViews(OverlayBattleViewTypes);
        }

        /// <summary>离关：Close 全部战斗 UI，0 秒延迟下一帧 Release。</summary>
        public static void ReleaseAllBattleViews()
        {
            CloseBattleViews(AllBattleViewTypes);
        }

        static void CloseBattleViews(Type[] viewTypes)
        {
            if (ViewManager.Instance == null)
            {
                return;
            }

            for (int i = 0; i < viewTypes.Length; i++)
            {
                ViewManager.Instance.CloseView(viewTypes[i]);
            }
        }
    }
}
