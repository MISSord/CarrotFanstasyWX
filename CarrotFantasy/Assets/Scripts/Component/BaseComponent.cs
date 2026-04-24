using ETModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
