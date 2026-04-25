using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class Zh_language
    {
        protected Dictionary<int, String> zhLangage = new Dictionary<int, string>();

        public Zh_language()
        {
            Init();
        }

        public virtual void Init()
        {

        }

        public string GetString(int id)
        {
            if (zhLangage[id] != null)
            {
                return zhLangage[id];
            }
            return null;
        }
    }
}
