namespace CarrotFantasy
{
    public sealed class NormalLevelListItem
    {
        public const string MapFilePathBase = "Pictures/GameOption/Normal/Level/";

        public SingleMapInfo mapInfo;

        public string GetLevelSpritePath(int bigLevelId)
        {
            return MapFilePathBase + bigLevelId + "/Level_" + mapInfo.levelId;
        }

        public string GetCarrotSpritePath()
        {
            return MapFilePathBase + "Carrot_" + mapInfo.carrotState;
        }
    }
}
