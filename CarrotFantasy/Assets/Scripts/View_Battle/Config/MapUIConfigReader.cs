using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public class MapUIConfigReader
    {
        public Dictionary<int, Dictionary<String, int>> mapUIParam = new Dictionary<int, Dictionary<string, int>>();

        public void Init()
        {
            this.mapUIParam.Add(101, new Dictionary<String, int>() {
                { "mapBg", 0},
                { "mapRoad", 1},
            });
            this.mapUIParam.Add(102, new Dictionary<String, int>() {
                { "mapBg", 0},
                { "mapRoad", 2},
            });
            this.mapUIParam.Add(103, new Dictionary<String, int>() {
                { "mapBg", 1},
                { "mapRoad", 3},
            });
            this.mapUIParam.Add(104, new Dictionary<String, int>() {
                { "mapBg", 1},
                { "mapRoad", 4},
            });
            this.mapUIParam.Add(105, new Dictionary<String, int>() {
                { "mapBg", 0},
                { "mapRoad", 5},
            });
        }

        public bool TryGetMapUIConfig(int bigLevel, int level, out Dictionary<String, int> config)
        {
            int key = bigLevel * 100 + level;
            if (this.mapUIParam.TryGetValue(key, out config))
            {
                return true;
            }

            if (this.mapUIParam.TryGetValue(101, out config))
            {
                return false;
            }

            config = new Dictionary<string, int>
            {
                { "mapBg", 0 },
                { "mapRoad", 1 },
            };
            return false;
        }

        public Dictionary<String, int> getMapUIConfig(int bigLevel, int level)
        {
            Dictionary<String, int> config;
            TryGetMapUIConfig(bigLevel, level, out config);
            return config;
        }
    }
}
