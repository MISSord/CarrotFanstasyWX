using UnityEngine;

#if UNITY_EDITOR
#endif

namespace CarrotFantasy
{
    public static class ResourcesHelper
    {
        public static UnityEngine.Object Load(string path)
        {
            return Resources.Load(path);
        }
    }
}
