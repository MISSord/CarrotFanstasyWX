namespace CarrotFantasy
{
    public abstract class BaseServer
    {
        public bool isFirstLoad = false;

        public virtual void Init() //初始化
        {

        }

        public virtual void LoadModule()
        {
            isFirstLoad = true;
        }

        public virtual void ReloadModule()
        {
            isFirstLoad = false;
        }

        public virtual void Dispose() { }

        public virtual void AddSocketListener() { }

        public virtual void RemoveSocketListener() { }
    }

    /// <summary>
    /// 统一服务单例基类。
    /// 子类继承 BaseServer&lt;T&gt; 后自动获得 T.Instance。
    /// </summary>
    /// <typeparam name="T">具体服务类型</typeparam>
    public abstract class BaseServer<T> : BaseServer where T : BaseServer<T>, new()
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance.OnSingletonInit();
                }
                return _instance;
            }
        }

        protected virtual void OnSingletonInit()
        {
        }
    }
}
