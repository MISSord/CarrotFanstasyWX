using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public delegate void CallBackOne(Dictionary<String, dynamic> param);
    public delegate void CallBack();
    public delegate void CallBack<T>(T arg);
    public delegate void CallBack<T, X>(T arg1, X arg2);
    public delegate void CallBack<T, X, Y>(T arg1, X arg2, Y arg3);
    public delegate void CallBack<T, X, Y, Z>(T arg1, X arg2, Y arg3, Z arg4);
    public delegate void CallBack<T, X, Y, Z, W>(T arg1, X arg2, Y arg3, Z arg4, W arg5);

    public class EventDispatcher //普通事件广播
    {
        private Dictionary<String, Delegate> eventTable = new Dictionary<String, Delegate>();

        private void OnListenerAdding(String eventType, Delegate callBack)
        {
            if (!eventTable.ContainsKey(eventType))
            {
                eventTable.Add(eventType, null);
            }
            Delegate d = eventTable[eventType];
            if (d != null && d.GetType() != callBack.GetType())
            {
                throw new Exception(string.Format("尝试为事件{0}添加不同类型的委托，当前事件所对应的委托是{1}，要添加的委托类型为{2}", eventType, d.GetType(), callBack.GetType()));
            }
        }
        private bool OnListenerRemoving(String eventType, Delegate callBack)
        {
            if (eventTable.ContainsKey(eventType))
            {
                Delegate d = eventTable[eventType];
                if (d == null)
                {
                    throw new Exception(string.Format("移除监听错误：事件{0}没有对应的委托", eventType));
                }
                else if (d.GetType() != callBack.GetType())
                {
                    throw new Exception(string.Format("移除监听错误：尝试为事件{0}移除不同类型的委托，当前委托类型为{1}，要移除的委托类型为{2}", eventType, d.GetType(), callBack.GetType()));
                }
                return true;
            }
            else
            {
                return false;
                //throw new Exception(string.Format("移除监听错误：没有事件码{0}", eventType));
            }
        }
        private void OnListenerRemoved(String eventType)
        {
            if (eventTable[eventType] == null)
            {
                eventTable.Remove(eventType);
            }
        }
        //no parameters
        public void AddListener(String eventType, CallBack callBack)
        {
            this.OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack)eventTable[eventType] + callBack;
        }
        //Single parameters
        public void AddListener<T>(String eventType, CallBack<T> callBack)
        {
            this.OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack<T>)eventTable[eventType] + callBack;
        }
        //two parameters
        public void AddListener<T, X>(String eventType, CallBack<T, X> callBack)
        {
            OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack<T, X>)eventTable[eventType] + callBack;
        }
        //three parameters
        public void AddListener<T, X, Y>(String eventType, CallBack<T, X, Y> callBack)
        {
            OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack<T, X, Y>)eventTable[eventType] + callBack;
        }
        //four parameters
        public void AddListener<T, X, Y, Z>(String eventType, CallBack<T, X, Y, Z> callBack)
        {
            OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack<T, X, Y, Z>)eventTable[eventType] + callBack;
        }
        //five parameters
        public void AddListener<T, X, Y, Z, W>(String eventType, CallBack<T, X, Y, Z, W> callBack)
        {
            OnListenerAdding(eventType, callBack);
            eventTable[eventType] = (CallBack<T, X, Y, Z, W>)eventTable[eventType] + callBack;
        }

        //no parameters
        public void RemoveListener(String eventType, CallBack callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }
        //single parameters
        public void RemoveListener<T>(String eventType, CallBack<T> callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack<T>)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }
        //two parameters
        public void RemoveListener<T, X>(String eventType, CallBack<T, X> callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack<T, X>)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }
        //three parameters
        public void RemoveListener<T, X, Y>(String eventType, CallBack<T, X, Y> callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack<T, X, Y>)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }
        //four parameters
        public void RemoveListener<T, X, Y, Z>(String eventType, CallBack<T, X, Y, Z> callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack<T, X, Y, Z>)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }
        //five parameters
        public void RemoveListener<T, X, Y, Z, W>(String eventType, CallBack<T, X, Y, Z, W> callBack)
        {
            if (OnListenerRemoving(eventType, callBack))
            {
                eventTable[eventType] = (CallBack<T, X, Y, Z, W>)eventTable[eventType] - callBack;
                OnListenerRemoved(eventType);
            }
        }


        //no parameters
        public void DispatchEvent(String eventType)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack callBack = d as CallBack;
                if (callBack != null)
                {
                    callBack();
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }
        //single parameters
        public void DispatchEvent<T>(String eventType, T arg)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack<T> callBack = d as CallBack<T>;
                if (callBack != null)
                {
                    callBack(arg);
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }
        //two parameters
        public void DispatchEvent<T, X>(String eventType, T arg1, X arg2)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack<T, X> callBack = d as CallBack<T, X>;
                if (callBack != null)
                {
                    callBack(arg1, arg2);
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }
        //three parameters
        public void DispatchEvent<T, X, Y>(String eventType, T arg1, X arg2, Y arg3)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack<T, X, Y> callBack = d as CallBack<T, X, Y>;
                if (callBack != null)
                {
                    callBack(arg1, arg2, arg3);
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }
        //four parameters
        public void DispatchEvent<T, X, Y, Z>(String eventType, T arg1, X arg2, Y arg3, Z arg4)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack<T, X, Y, Z> callBack = d as CallBack<T, X, Y, Z>;
                if (callBack != null)
                {
                    callBack(arg1, arg2, arg3, arg4);
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }
        //five parameters
        public void DispatchEvent<T, X, Y, Z, W>(String eventType, T arg1, X arg2, Y arg3, Z arg4, W arg5)
        {
            Delegate d;
            if (eventTable.TryGetValue(eventType, out d))
            {
                CallBack<T, X, Y, Z, W> callBack = d as CallBack<T, X, Y, Z, W>;
                if (callBack != null)
                {
                    callBack(arg1, arg2, arg3, arg4, arg5);
                }
                else
                {
                    throw new Exception(string.Format("广播事件错误：事件{0}对应委托具有不同的类型", eventType));
                }
            }
        }


        public void Dispose()
        {
            this.eventTable.Clear();
        }

    }
}




