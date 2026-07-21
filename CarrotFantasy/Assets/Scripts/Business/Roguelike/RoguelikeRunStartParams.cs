using System;

namespace CarrotFantasy
{
    /// <summary>由选关层组装、交给 <see cref="RoguelikeRunServer.StartRun"/> 的开局快照。</summary>
    public class RoguelikeRunStartParams
    {
        public int bigLevelId;
        public int levelId;
        public int mapId;
        public string hexMapAssetId;
        public int shopPoolId;
        public int startingGold;
        public int[] startingEffectIds;
        public int encounterTableId;
        public int randomEventPoolId;
        public int runSeed;

        public HexWorldProgress mapProgress;

        public static RoguelikeRunStartParams FromLevelDef(RoguelikeLevelDef def, int mapId = 0)
        {
            if (def == null)
            {
                throw new ArgumentNullException("def");
            }

            return new RoguelikeRunStartParams
            {
                bigLevelId = def.bigLevelId,
                levelId = def.levelId,
                mapId = mapId,
                hexMapAssetId = def.hexMapAssetId,
                shopPoolId = def.shopPoolId,
                startingGold = def.startingGold,
                startingEffectIds = def.startingEffectIds != null
                    ? (int[])def.startingEffectIds.Clone()
                    : Array.Empty<int>(),
                encounterTableId = def.encounterTableId,
                randomEventPoolId = def.randomEventPoolId,
            };
        }
    }
}
