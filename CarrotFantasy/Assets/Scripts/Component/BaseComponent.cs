namespace CarrotFantasy
{
    public abstract class BaseComponent
    {
        public bool IsDisposed = false;

        public virtual void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }
        }
    }
}
