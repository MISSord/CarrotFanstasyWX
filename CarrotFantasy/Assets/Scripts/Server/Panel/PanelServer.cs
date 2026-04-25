using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary> 原面板队列逻辑已弃用，保留 eventDispatcher 与切场景时关闭 View。 </summary>
    public class PanelServer
    {
        public EventDispatcher eventDispatcher;
        private bool isCanShowPanel = true;

        public void Init()
        {
            this.eventDispatcher = new EventDispatcher();
            ServerProvision.sceneServer.GetEventDispatcher().AddListener(SceneEventType.LOAD_SCENE_FINISH, this.sceneLoadFinishCallBack);
        }

        private void sceneLoadFinishCallBack()
        {
        }

        public void SetShowPanelActive(bool isCanShow)
        {
            this.isCanShowPanel = isCanShow;
        }

        [Obsolete("使用 UIViewService 打开 BaseView，不再走 BasePanel")]
        public void ShowPanel(BasePanel targetPanel)
        {
            Debug.LogWarning("PanelServer.ShowPanel 已弃用，请使用 UIViewService。");
        }

        public void CloseAllPanel(int closeReason, BaseSceneType nextSceneType)
        {
            UIViewService.CloseAllViews();
        }

        public void ClosePanel(int uid, int closeReason)
        {
        }

        public void Dispose()
        {
        }
    }
}
