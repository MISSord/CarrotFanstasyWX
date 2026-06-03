using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 单个地图点位的静态配置（稀疏存储，仅 Path / Event）。
/// 使用轴向坐标 (q, r)，与 HexCoordinates 一致。
/// </summary>
[Serializable]
public struct HexMapPointData
{
	/// <summary>
	/// 由 (q, r, mapWidth) 推导的稳定 id；Save/Load 时会与坐标重新同步。
	/// </summary>
	public int pointId;

	/// <summary>轴向坐标 q（对应 HexCoordinates.X）。</summary>
	public int q;

	/// <summary>轴向坐标 r（对应 HexCoordinates.Z）。</summary>
	public int r;

	public HexPointKind kind;

	/// <summary>Path 点为 None；Event 点决定 HexEventDispatcher 分支。</summary>
	[FormerlySerializedAs("eventTypeId")]
	public HexEventKind eventKind;

	/// <summary>JSON 字符串，如 encounterId、targetQ/targetR、randomEventId。</summary>
	public string payload;

	/// <summary>类型表未覆盖时的触发时机 fallback。</summary>
	public TriggerTiming triggerTiming;

	/// <summary>类型表未覆盖时的触发后规则 fallback（仅 OnLeave 链路的 ApplyPostTrigger 使用）。</summary>
	public PostTriggerRule postRule;

	public HexCoordinates Coordinates {
		get { return new HexCoordinates(q, r); }
	}

	public void SyncPointId (int mapWidth)
	{
		pointId = HexMapPointId.Encode(q, r, mapWidth);
	}

	public bool ValidatePointId (int mapWidth, out int expectedId)
	{
		expectedId = HexMapPointId.Encode(q, r, mapWidth);
		return pointId == expectedId;
	}
}
