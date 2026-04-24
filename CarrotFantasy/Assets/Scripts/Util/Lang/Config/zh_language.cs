using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class zh_language
    {
        protected Dictionary<int, String> zhLangage = new Dictionary<int, string>();

        public zh_language()
        {
            init();
        }

        public virtual void init()
        {

        }

        public string getString(int id)
        {
            if (zhLangage[id] != null)
            {
                return zhLangage[id];
            }
            return null;
        }
    }
}
