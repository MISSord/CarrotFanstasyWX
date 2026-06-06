using System.Collections.Generic;
using cfg;
using UnityEngine;

namespace CarrotFantasy
{
    public class BuffConfigReader
    {
        static BuffConfigReader instance;
        readonly Dictionary<int, BuffDef> buffDefs = new Dictionary<int, BuffDef>();

        public static BuffConfigReader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BuffConfigReader();
                    instance.Init();
                }

                return instance;
            }
        }

        public void Init()
        {
            this.buffDefs.Clear();

            foreach (cfg.BuffDef def in LubanConfigLoader.Tables.TbBuff.DataList)
            {
                this.buffDefs[def.BuffId] = new BuffDef
                {
                    id = def.BuffId,
                    category = (BuffCategory)(int)def.Category,
                    duration = new Fix64(def.Duration),
                    tickInterval = new Fix64(def.TickInterval),
                    tickDamage = def.TickDamage,
                    param0 = new Fix64(def.Param0),
                };
            }
        }

        public bool TryGetDef(int buffId, out BuffDef def)
        {
            return this.buffDefs.TryGetValue(buffId, out def);
        }
    }
}
