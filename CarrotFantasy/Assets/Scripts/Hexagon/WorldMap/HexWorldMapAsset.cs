using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 大地图静态布局资源。只存有内容的 Path / Event 点，空点不写入 points。
/// </summary>
[CreateAssetMenu(menuName = "Hex/World Map")]
public class HexWorldMapAsset : ScriptableObject
{
	/// <summary>地图边界宽（轴向布局参考，用于编辑器与范围校验）。</summary>
	public int width = 30;

	/// <summary>地图边界高。</summary>
	public int height = 30;

	/// <summary>起点（Start 事件格）轴向坐标 q，由 SyncPlayerStartFromStartPoint 与 Start 格同步。</summary>
	public int playerStartQ;

	/// <summary>起点（Start 事件格）轴向坐标 r。</summary>
	public int playerStartR;

	[FormerlySerializedAs("playerStartPointId")]
	[HideInInspector]
	public int legacyPlayerStartPointId;

	/// <summary>稀疏点位列表，大地图中通常仅占约 10% 的格子。</summary>
	public List<HexMapPointData> points = new List<HexMapPointData>();

	public int GetPlayerStartPointId ()
	{
		return HexMapPointId.Encode(playerStartQ, playerStartR, width);
	}

	public void SyncAllPointIds ()
	{
		for (int i = 0; i < points.Count; i++) {
			HexMapPointData point = points[i];
			point.SyncPointId(width);
			points[i] = point;
		}
	}

	/// <summary>将 playerStartQ/R 写为唯一 Start 事件格坐标；无 Start 或存在多个时返回 false。</summary>
	public bool SyncPlayerStartFromStartPoint ()
	{
		int startCount = 0;
		HexMapPointData startPoint = default;

		for (int i = 0; i < points.Count; i++) {
			HexMapPointData point = points[i];
			if (point.kind != HexPointKind.Event || point.eventKind != HexEventKind.Start) {
				continue;
			}
			startCount++;
			startPoint = point;
		}

		if (startCount != 1) {
			return false;
		}

		playerStartQ = startPoint.q;
		playerStartR = startPoint.r;
		return true;
	}

	public bool TryGetStartPoint (out HexMapPointData startPoint)
	{
		startPoint = default;
		int startCount = 0;

		for (int i = 0; i < points.Count; i++) {
			HexMapPointData point = points[i];
			if (point.kind != HexPointKind.Event || point.eventKind != HexEventKind.Start) {
				continue;
			}
			startCount++;
			startPoint = point;
		}

		return startCount == 1;
	}

	public bool TryGetPointAt (int q, int r, out HexMapPointData point)
	{
		for (int i = 0; i < points.Count; i++) {
			HexMapPointData candidate = points[i];
			if (candidate.q == q && candidate.r == r) {
				point = candidate;
				return true;
			}
		}
		point = default;
		return false;
	}

	public bool TryResolvePlayerStartPointId (out int pointId)
	{
		if (TryGetStartPoint(out HexMapPointData start)) {
			playerStartQ = start.q;
			playerStartR = start.r;
			pointId = HexMapPointId.Encode(start.q, start.r, width);
			return true;
		}

		if (TryGetPointAt(playerStartQ, playerStartR, out HexMapPointData atCoords)) {
			pointId = HexMapPointId.Encode(atCoords.q, atCoords.r, width);
			return true;
		}

		if (legacyPlayerStartPointId != 0) {
			for (int i = 0; i < points.Count; i++) {
				if (points[i].pointId == legacyPlayerStartPointId) {
					HexMapPointData legacy = points[i];
					playerStartQ = legacy.q;
					playerStartR = legacy.r;
					pointId = HexMapPointId.Encode(legacy.q, legacy.r, width);
					return true;
				}
			}
		}

		pointId = 0;
		return false;
	}
}
