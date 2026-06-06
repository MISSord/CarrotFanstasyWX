using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    public class TowerConfigReader
    {
        public Dictionary<int, Dictionary<string, Fix64>> towerBirthParam = new Dictionary<int, Dictionary<string, Fix64>>();

        static TowerConfigReader reader;

        public static TowerConfigReader Instance
        {
            get
            {
                if (reader == null)
                {
                    reader = new TowerConfigReader();
                    reader.Init();
                }

                return reader;
            }
        }

        public void Init()
        {
            this.towerBirthParam.Clear();

            foreach (TowerDef def in LubanConfigLoader.Tables.TbTower.DataList)
            {
                this.towerBirthParam[def.TowerId] = ToBirthParam(def);
            }
        }

        static Dictionary<string, Fix64> ToBirthParam(TowerDef def)
        {
            return new Dictionary<string, Fix64>
            {
                { "towerID", new Fix64(def.TowerId) },
                { "price0", new Fix64(def.Price1) },
                { "price1", new Fix64(def.Price2) },
                { "price2", new Fix64(def.Price3) },
                { "attackCD", new Fix64(def.AttackCd) },
                { "bodyRadius0", new Fix64(def.BodyRadius1) },
                { "bodyRadius1", new Fix64(def.BodyRadius2) },
                { "bodyRadius2", new Fix64(def.BodyRadius3) },
                { "scale", new Fix64(def.Scale) },
            };
        }

        public Dictionary<string, Fix64> GetSingleTowerConfig(int id)
        {
            if (this.towerBirthParam.TryGetValue(id, out Dictionary<string, Fix64> param))
            {
                return param;
            }

            Debug.LogError("Tower config missing: " + id);
            return null;
        }
    }
}
