using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class ConnectEventDispatcher
    {
        private EventDispatcher eventDispatcher;

        public ConnectEventDispatcher()
        {
            Init();
        }

        public void Init()
        {
            eventDispatcher = new EventDispatcher();
        }

        public EventDispatcher GetEventDispatcher()
        {
            return eventDispatcher;
        }

        public void DispatcherConnectEvent(int eventType, Dictionary<String, dynamic> mrg)
        {
            eventDispatcher.DispatchEvent(eventType.ToString(), mrg);
        }

        public void Dispose()
        {
            eventDispatcher.Dispose();
        }

        public void AddConnectListener(int eventType, CallBack callBack)
        {
            eventDispatcher.AddListener(eventType.ToString(), callBack);
        }

        public void RemoveConnectListener(int eventType, CallBack callBack)
        {
            eventDispatcher.RemoveListener(eventType.ToString(), callBack);
        }
    }
}
