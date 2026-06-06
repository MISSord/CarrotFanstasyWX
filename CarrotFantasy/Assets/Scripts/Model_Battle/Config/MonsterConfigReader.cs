using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    public class MonsterConfigReader
    {
        public Dictionary<int, Dictionary<string, Fix64>> monsterBirthParam = new Dictionary<int, Dictionary<string, Fix64>>();

        static MonsterConfigReader instance;

        public static MonsterConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MonsterConfigReader();
                    instance.Init();
                }

                return instance;
            }
        }

        public void Init()
        {
            this.monsterBirthParam.Clear();

            foreach (MonsterDef def in LubanConfigLoader.Tables.TbMonster.DataList)
            {
                this.monsterBirthParam[def.MonsterId] = ToBirthParam(def);
            }
        }

        static Dictionary<string, Fix64> ToBirthParam(MonsterDef def)
        {
            Fix64 bodyRadius = def.MonsterId == 12 ? new Fix64(0.6f) : new Fix64(0.3f);

            return new Dictionary<string, Fix64>
            {
                { "faceDirection", new Fix64(def.FaceDirection) },
                { "bodyRadius", bodyRadius },
                { "scale", new Fix64(def.Scale) },
                { "offsetX", new Fix64(def.OffsetX) },
                { "offsetY", new Fix64(def.OffsetY) },
                { "speed", Fix64.One },
                { "live", new Fix64(def.Hp) },
            };
        }

        public Dictionary<string, Fix64> GetSingleMonsterConfig(int monsterId)
        {
            if (this.monsterBirthParam.TryGetValue(monsterId, out Dictionary<string, Fix64> param))
            {
                return param;
            }

            Debug.LogError("Monster config missing: " + monsterId);
            return null;
        }
    }
}
