using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    public class BulletConfigReader
    {
        public Dictionary<int, Dictionary<string, Fix64>> bulletBirthParam = new Dictionary<int, Dictionary<string, Fix64>>();

        static BulletConfigReader instance;

        public static BulletConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BulletConfigReader();
                    instance.Init();
                }

                return instance;
            }
        }

        public void Init()
        {
            this.bulletBirthParam.Clear();

            foreach (BulletDef def in LubanConfigLoader.Tables.TbBullet.DataList)
            {
                this.bulletBirthParam[def.BulletId] = ToBirthParam(def);
            }
        }

        static Dictionary<string, Fix64> ToBirthParam(BulletDef def)
        {
            TowerDef towerDef = LubanConfigLoader.Tables.TbTower.GetOrDefault(def.TowerId);
            BulletMoveType moveType = towerDef != null
                ? BulletMoveComponentFactory.Normalize(towerDef.BulletMoveType)
                : BulletMoveType.Homing;

            Dictionary<string, Fix64> param = new Dictionary<string, Fix64>
            {
                { "faceDirection", Fix64.Zero },
                { "bodyRadius", new Fix64(def.BodyRadius) },
                { "scale", new Fix64(def.Scale) },
                { "speed", new Fix64(def.Speed) },
                { "damage", new Fix64(def.Damage) },
                { "moveSpeed", new Fix64(def.MoveSpeed) },
                { "isRemove", new Fix64(def.IsRemove) },
                { "bulletMoveType", new Fix64((int)moveType) },
            };

            if (def.OnHitBuffId != 0)
            {
                param["onHitBuffId"] = new Fix64(def.OnHitBuffId);
            }

            return param;
        }

        public Dictionary<string, Fix64> GetSingleBulletConfig(int id)
        {
            if (this.bulletBirthParam.TryGetValue(id, out Dictionary<string, Fix64> param))
            {
                return param;
            }

            Debug.LogError("Bullet config missing: " + id);
            return null;
        }
    }
}
