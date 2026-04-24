using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class ConnectEventDispatcher
    {
        private EventDispatcher eventDispatcher;

        public ConnectEventDispatcher()
        {
            init();
        }

        public void init()
        {
            eventDispatcher = new EventDispatcher();
        }

        public EventDispatcher getEventDispatcher()
        {
            return eventDispatcher;
        }

        public void dispatcherConnectEvent(int eventType, Dictionary<String, dynamic> mrg)
        {
            eventDispatcher.DispatchEvent(eventType.ToString(), mrg);
        }

        public void Dispose()
        {
            eventDispatcher.Dispose();
        }

        public void addConnectListener(int eventType, CallBack callBack)
        {
            eventDispatcher.AddListener(eventType.ToString(), callBack);
        }

        public void removeConnectListener(int eventType, CallBack callBack)
        {
            eventDispatcher.RemoveListener(eventType.ToString(), callBack);
        }
    }
}
