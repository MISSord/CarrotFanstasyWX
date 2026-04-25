using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarrotFantasy
{
    public delegate void LiftCycleFunc();

    public class PanelManagerUnit
    {
        private PanelStateType panelStateType;
        private Dictionary<String, LiftCycleFunc> liftCycleExeMap = new Dictionary<string, LiftCycleFunc>(); //可以写成链表形式进行有序可控的执行。
        private GameObject panelGameObject;

        public PanelManagerUnit(GameObject panelObject)
        {
            this.panelGameObject = panelObject;
        }

        public void SetState(PanelStateType state)
        {
            this.panelStateType = state;
        }

        public PanelStateType GetState()
        {
            return this.panelStateType;
        }

        public void OnAssetReady()
        {
            this.SetState(PanelStateType.init_done);
            this.CallLiftCycleFunc(PanelLifeCycleType.ON_ASSET_READY);
        }
        public void OnResume()
        {
            this.SetState(PanelStateType.active);
            this.CallLiftCycleFunc(PanelLifeCycleType.ON_RESUME);
        }
        public void OnPause()
        {
            this.SetState(PanelStateType.pending);
            this.CallLiftCycleFunc(PanelLifeCycleType.ON_PAUSE);
        }
        public void OnDestroy()
        {
            this.SetState(PanelStateType.disable);
            this.CallLiftCycleFunc(PanelLifeCycleType.ON_DESTROY);
        }

        public void CallLiftCycleFunc(String funcName)
        {
            LiftCycleFunc func = null;
            if (liftCycleExeMap.TryGetValue(funcName, out func))
            {
                func();
            }
        }
        private void RegisterLiftCycleEvent(String key, LiftCycleFunc func)
        {
            LiftCycleFunc fun = null;
            if (liftCycleExeMap.TryGetValue(key, out fun))
            {
                fun += func;
            }
            else
            {
                fun = func;
            }
            liftCycleExeMap[key] = fun;
        }
        public void RegisterOnAssetReady(LiftCycleFunc func)
        {
            this.RegisterLiftCycleEvent(PanelLifeCycleType.ON_ASSET_READY, func);
        }
        public void RegisterOnResume(LiftCycleFunc func)
        {
            this.RegisterLiftCycleEvent(PanelLifeCycleType.ON_RESUME, func);
        }
        public void RegisterOnPause(LiftCycleFunc func)
        {
            this.RegisterLiftCycleEvent(PanelLifeCycleType.ON_PAUSE, func);
        }
        public void RegisterOnDestroy(LiftCycleFunc func)
        {
            this.RegisterLiftCycleEvent(PanelLifeCycleType.ON_DESTROY, func);
        }


    }
}
