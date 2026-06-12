using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public static class MonsterIdsParser
    {
        public static List<int> Parse(string monsterIds)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(monsterIds))
            {
                return result;
            }

            string[] parts = monsterIds.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (!int.TryParse(part, out int id))
                {
                    throw new FormatException(string.Format("monsterIds 含非法 ID: \"{0}\"（原始: \"{1}\"）", part, monsterIds));
                }

                result.Add(id);
            }

            return result;
        }
    }
}
