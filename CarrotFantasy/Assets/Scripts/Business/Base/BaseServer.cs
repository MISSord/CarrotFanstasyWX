namespace CarrotFantasy
{
    public abstract class BaseServer
    {
        public bool isFirstLoad = false;

        public BaseServer()
        {

        }

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
}
