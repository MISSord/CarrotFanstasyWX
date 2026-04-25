using System;
using UnityEngine;

namespace CarrotFantasy
{
    public class ResourceLoader
    {
        private static ResourceLoader _resourceLoader;
        public static ResourceLoader Instance
        {
            get
            {
                if (_resourceLoader == null)
                {
                    _resourceLoader = new ResourceLoader();
                }
                return _resourceLoader;
            }
        }

        public GameObject getGameObject(String path)
        {
            return Resources.Load<GameObject>(path);
        }

        public T loadRes<T>(String path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }

    }
}
