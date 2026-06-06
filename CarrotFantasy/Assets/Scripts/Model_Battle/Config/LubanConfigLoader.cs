using cfg;
using SimpleJSON;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>Luban 配置表加载（Resources/Config/Luban/*.json）。</summary>
    public static class LubanConfigLoader
    {
        const string ResourceDir = "Config/Luban/";

        static Tables tables;

        public static Tables Tables
        {
            get
            {
                if (tables == null)
                {
                    tables = new Tables(LoadJson);
                }

                return tables;
            }
        }

        static JSONNode LoadJson(string file)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceDir + file);
            if (asset == null)
            {
                Debug.LogError("Luban config not found: " + ResourceDir + file);
                return JSONNull.Instance;
            }

            return JSONNode.Parse(asset.text);
        }
    }
}
