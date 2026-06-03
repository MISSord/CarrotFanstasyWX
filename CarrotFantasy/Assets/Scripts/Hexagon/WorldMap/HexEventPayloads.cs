using System;

/// <summary>战斗事件点的每点专属数据，序列化进 HexMapPointData.payload。</summary>
[Serializable]
public class BattleEventPayload
{
	public int encounterId;
}

/// <summary>传送事件点目标坐标（双向/单向起点共用）。</summary>
[Serializable]
public class TeleportEventPayload
{
	public int targetQ;
	public int targetR;

	/// <summary>旧版按顺序 id 存储，仅作迁移 fallback。</summary>
	public int targetPointId;

	public int ResolveTargetPointId (int mapWidth)
	{
		if (targetPointId != 0 && targetQ == 0 && targetR == 0) {
			return targetPointId;
		}

		return HexMapPointId.Encode(targetQ, targetR, mapWidth);
	}
}

/// <summary>随机事件点的每点专属数据，由 randomEventId 查随机事件表。</summary>
[Serializable]
public class RandomEventPayload
{
	public int randomEventId;
}
