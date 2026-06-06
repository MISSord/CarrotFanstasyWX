using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    public class ItemConfigReader
    {
        public Dictionary<int, Dictionary<string, Fix64>> itemBirthParam = new Dictionary<int, Dictionary<string, Fix64>>();

        static ItemConfigReader instance;

        public static ItemConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ItemConfigReader();
                    instance.Init();
                }

                return instance;
            }
        }

        public void Init()
        {
            this.itemBirthParam.Clear();

            foreach (ItemDef def in LubanConfigLoader.Tables.TbItem.DataList)
            {
                this.itemBirthParam[def.ItemTypeId] = ToBirthParam(def);
            }
        }

        static Dictionary<string, Fix64> ToBirthParam(ItemDef def)
        {
            return new Dictionary<string, Fix64>
            {
                { "faceDirection", new Fix64(def.FaceDirection) },
                { "bodyRadius", new Fix64(def.BodyRadius) },
                { "scale", new Fix64(def.Scale) },
                { "offsetX", new Fix64(def.OffsetX) },
                { "offsetY", new Fix64(def.OffsetY) },
                { "live", new Fix64(def.Live) },
                { "money", new Fix64(def.Money) },
            };
        }

        public Dictionary<string, Fix64> GetSingleItemConfig(int itemTypeId)
        {
            if (this.itemBirthParam.TryGetValue(itemTypeId, out Dictionary<string, Fix64> param))
            {
                return param;
            }

            Debug.LogError("Item config missing: " + itemTypeId);
            return null;
        }
    }
}
