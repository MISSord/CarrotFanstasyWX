using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 校验用只读上下文：坐标索引、走边邻接、传送边。
/// </summary>
public sealed class HexWorldMapValidationContext
{
	public HexWorldMapAsset asset;
	public readonly Dictionary<(int q, int r), HexMapPointData> coordToPoint =
		new Dictionary<(int q, int r), HexMapPointData>();
	public readonly Dictionary<int, HexMapPointData> pointIdToPoint =
		new Dictionary<int, HexMapPointData>();
	public readonly List<int>[] walkNeighborPointIds;
	public readonly List<(int fromPointId, int toPointId)> teleportEdges =
		new List<(int, int)>();

	HexWorldMapValidationContext () { }

	HexWorldMapValidationContext (HexWorldMapAsset mapAsset)
	{
		this.asset = mapAsset;
		int count = mapAsset.points.Count;
		walkNeighborPointIds = new List<int>[count];

		for (int i = 0; i < count; i++) {
			HexMapPointData point = mapAsset.points[i];
			var key = (point.q, point.r);
			if (!coordToPoint.ContainsKey(key)) {
				coordToPoint[key] = point;
			}
			if (!pointIdToPoint.ContainsKey(point.pointId)) {
				pointIdToPoint[point.pointId] = point;
			}
		}

		for (int i = 0; i < count; i++) {
			walkNeighborPointIds[i] = BuildWalkNeighbors(mapAsset, i, coordToPoint);
		}

		for (int i = 0; i < count; i++) {
			HexMapPointData point = mapAsset.points[i];
			if (point.kind != HexPointKind.Event) {
				continue;
			}

			int fromId = point.pointId;
			switch (point.eventKind) {
			case HexEventKind.Teleport:
			case HexEventKind.OneWayTeleportStart:
				if (TryResolveTeleportTarget(mapAsset, point, this, out int toId)) {
					teleportEdges.Add((fromId, toId));
				}
				break;
			}
		}
	}

	public int PointCount {
		get { return asset != null ? asset.points.Count : 0; }
	}

	public static HexWorldMapValidationContext Build (HexWorldMapAsset mapAsset)
	{
		if (mapAsset == null) {
			return new HexWorldMapValidationContext();
		}

		return new HexWorldMapValidationContext(mapAsset);
	}

	static List<int> BuildWalkNeighbors (
		HexWorldMapAsset mapAsset,
		int pointIndex,
		Dictionary<(int q, int r), HexMapPointData> coordLookup
	)
	{
		var neighbors = new List<int>();
		HexMapPointData point = mapAsset.points[pointIndex];
		HexCoordinates coordinates = point.Coordinates;

		for (int d = 0; d < HexCoordinates.Directions.Length; d++) {
			HexCoordinates neighborCoord = coordinates.GetNeighbor(d);
			HexMapPointData neighbor;
			if (coordLookup.TryGetValue((neighborCoord.X, neighborCoord.Z), out neighbor)) {
				neighbors.Add(neighbor.pointId);
			}
		}

		return neighbors;
	}

	public bool TryGetPointAt (int q, int r, out HexMapPointData point)
	{
		return coordToPoint.TryGetValue((q, r), out point);
	}

	public bool TryGetPointById (int pointId, out HexMapPointData point)
	{
		return pointIdToPoint.TryGetValue(pointId, out point);
	}

	public int IndexOfPointId (int pointId)
	{
		for (int i = 0; i < asset.points.Count; i++) {
			if (asset.points[i].pointId == pointId) {
				return i;
			}
		}
		return -1;
	}

	public static bool IsWithinMapBounds (HexWorldMapAsset mapAsset, int q, int r)
	{
		int col = q + r / 2;
		int row = r;
		return col >= 0 && col < mapAsset.width && row >= 0 && row < mapAsset.height;
	}

	public static bool TryResolveTeleportTarget (
		HexWorldMapAsset mapAsset,
		HexMapPointData from,
		HexWorldMapValidationContext ctx,
		out int targetPointId
	)
	{
		targetPointId = 0;
		if (string.IsNullOrEmpty(from.payload)) {
			return false;
		}

		TeleportEventPayload payload;
		try {
			payload = JsonUtility.FromJson<TeleportEventPayload>(from.payload);
		}
		catch {
			return false;
		}

		if (payload == null) {
			return false;
		}

		if (payload.targetQ != 0 || payload.targetR != 0) {
			HexMapPointData target;
			if (!ctx.TryGetPointAt(payload.targetQ, payload.targetR, out target)) {
				return false;
			}
			targetPointId = target.pointId;
			return true;
		}

		if (payload.targetPointId != 0) {
			if (ctx.TryGetPointById(payload.targetPointId, out _)) {
				targetPointId = payload.targetPointId;
				return true;
			}
		}

		int encoded = payload.ResolveTargetPointId(mapAsset.width);
		if (ctx.TryGetPointById(encoded, out _)) {
			targetPointId = encoded;
			return true;
		}

		return false;
	}
}
