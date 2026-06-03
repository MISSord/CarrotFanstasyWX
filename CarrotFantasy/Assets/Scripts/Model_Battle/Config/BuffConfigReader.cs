using System.Collections.Generic;

namespace CarrotFantasy
{
    public class BuffConfigReader
    {
        private static BuffConfigReader instance;
        private readonly Dictionary<int, BuffDef> buffDefs = new Dictionary<int, BuffDef>();

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
            if (this.buffDefs.Count > 0)
            {
                return;
            }

            AddDef(1001, BuffCategory.Slow, new Fix64(3f), Fix64.Zero, 0, new Fix64(0.35f));
            AddDef(1002, BuffCategory.Dot, new Fix64(5f), Fix64.One, 2, Fix64.Zero);
            AddDef(1003, BuffCategory.Stun, new Fix64(2f), Fix64.Zero, 0, Fix64.Zero);
        }

        private void AddDef(int id, BuffCategory category, Fix64 duration, Fix64 tickInterval, int tickDamage, Fix64 param0)
        {
            this.buffDefs.Add(id, new BuffDef
            {
                id = id,
                category = category,
                duration = duration,
                tickInterval = tickInterval,
                tickDamage = tickDamage,
                param0 = param0,
            });
        }

        public bool TryGetDef(int buffId, out BuffDef def)
        {
            return this.buffDefs.TryGetValue(buffId, out def);
        }
    }
}
