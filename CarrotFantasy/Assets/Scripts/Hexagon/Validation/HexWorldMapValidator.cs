using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HexWorldMapAsset 保存前配置校验（MVP 规则集）。
/// </summary>
public static class HexWorldMapValidator
{
	public static HexWorldMapValidationReport Validate (HexWorldMapAsset asset)
	{
		var report = new HexWorldMapValidationReport();
		if (asset == null) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"MAP_NULL",
				"地图资源为空。"
			));
			return report;
		}

		HexWorldMapValidationContext ctx = HexWorldMapValidationContext.Build(asset);

		ValidateStructure(asset, ctx, report);
		ValidatePointIds(asset, ctx, report);
		ValidateEventPayloads(asset, ctx, report);
		ValidateTeleportRules(asset, ctx, report);
		ValidateConnectivity(asset, ctx, report);
		ValidateStartAndFinal(asset, ctx, report);

		return report;
	}

	static void ValidateStructure (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		if (asset.points.Count == 0) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"MAP_EMPTY",
				"地图没有任何有效点位。"
			));
			return;
		}

		var coordCount = new Dictionary<(int q, int r), int>();
		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];

			var key = (point.q, point.r);
			int count;
			if (!coordCount.TryGetValue(key, out count)) {
				count = 0;
			}
			coordCount[key] = count + 1;
			if (count >= 1) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Error,
					"COORD_DUPLICATE",
					"同一坐标存在多个点位。",
					point,
					true
				));
			}

			if (!HexWorldMapValidationContext.IsWithinMapBounds(asset, point.q, point.r)) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Error,
					"COORD_OUT_OF_BOUNDS",
					"点位超出地图 width/height 范围。",
					point,
					true
				));
			}
		}
	}

	static void ValidatePointIds (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			int expectedId;
			if (!point.ValidatePointId(asset.width, out expectedId)) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Warning,
					"POINT_ID_MISMATCH",
					"pointId 与坐标不一致（保存时会自动 Sync）。stored=" +
					point.pointId + " expected=" + expectedId,
					point,
					true
				));
			}
		}
	}

	static void ValidateEventPayloads (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.kind != HexPointKind.Event) {
				continue;
			}

			switch (point.eventKind) {
			case HexEventKind.Battle:
				ValidateBattlePayload(point, report);
				break;
			case HexEventKind.Teleport:
			case HexEventKind.OneWayTeleportStart:
				ValidateTeleportPayload(asset, point, ctx, report);
				break;
			case HexEventKind.Random:
				ValidateRandomPayload(point, report);
				break;
			}
		}
	}

	static void ValidateBattlePayload (HexMapPointData point, HexWorldMapValidationReport report)
	{
		if (string.IsNullOrEmpty(point.payload)) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"BATTLE_NO_PAYLOAD",
				"战斗点缺少 payload（需 encounterId）。",
				point,
				true
			));
			return;
		}

		BattleEventPayload payload = ParsePayload<BattleEventPayload>(point.payload);
		if (payload == null || payload.encounterId <= 0) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"BATTLE_NO_ENCOUNTER",
				"战斗点 encounterId 无效（需 > 0）。",
				point,
				true
			));
		}
	}

	static void ValidateRandomPayload (HexMapPointData point, HexWorldMapValidationReport report)
	{
		if (string.IsNullOrEmpty(point.payload)) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"RANDOM_NO_PAYLOAD",
				"随机事件点缺少 payload（需 randomEventId）。",
				point,
				true
			));
			return;
		}

		RandomEventPayload payload = ParsePayload<RandomEventPayload>(point.payload);
		if (payload == null || payload.randomEventId <= 0) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"RANDOM_NO_EVENT_ID",
				"随机事件点 randomEventId 无效（需 > 0）。",
				point,
				true
			));
		}
	}

	static void ValidateTeleportPayload (
		HexWorldMapAsset asset,
		HexMapPointData point,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		if (string.IsNullOrEmpty(point.payload)) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"TELEPORT_NO_PAYLOAD",
				"传送点缺少 payload（需 targetQ/targetR）。",
				point,
				true
			));
			return;
		}

		TeleportEventPayload payload = ParsePayload<TeleportEventPayload>(point.payload);
		if (payload == null) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"PAYLOAD_JSON_INVALID",
				"传送点 payload JSON 无法解析。",
				point,
				true
			));
			return;
		}

		if (payload.targetQ != 0 || payload.targetR != 0) {
			if (!ctx.TryGetPointAt(payload.targetQ, payload.targetR, out _)) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Error,
					"TELEPORT_TARGET_MISSING",
					"传送目标坐标 (" + payload.targetQ + "," + payload.targetR +
					") 在地图上不存在。",
					point,
					true
				));
			}
			return;
		}

		if (payload.targetPointId != 0) {
			if (!ctx.TryGetPointById(payload.targetPointId, out _)) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Error,
					"TELEPORT_TARGET_MISSING",
					"传送目标 pointId " + payload.targetPointId + " 不存在。",
					point,
					true
				));
			}
			return;
		}

		report.Add(HexWorldMapValidationIssue.Create(
			HexWorldMapValidationSeverity.Error,
			"TELEPORT_TARGET_MISSING",
			"传送点未配置有效目标（targetQ/targetR 或 targetPointId）。",
			point,
			true
		));
	}

	static void ValidateTeleportRules (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		int bidirectionalTeleportCount = 0;
		for (int i = 0; i < asset.points.Count; i++) {
			if (asset.points[i].eventKind == HexEventKind.Teleport) {
				bidirectionalTeleportCount++;
			}
		}

		if (bidirectionalTeleportCount == 1) {
			for (int i = 0; i < asset.points.Count; i++) {
				HexMapPointData point = asset.points[i];
				if (point.eventKind == HexEventKind.Teleport) {
					report.Add(HexWorldMapValidationIssue.Create(
						HexWorldMapValidationSeverity.Warning,
						"TELEPORT_SINGLE_ENDPOINT",
						"全图仅有一个双向传送点，无法构成双向传送。",
						point,
						true
					));
					break;
				}
			}
		}

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.eventKind != HexEventKind.Teleport) {
				continue;
			}

			TeleportEventPayload payload = ParsePayload<TeleportEventPayload>(point.payload);
			if (payload == null || (payload.targetQ == 0 && payload.targetR == 0)) {
				continue;
			}

			HexMapPointData target;
			if (!ctx.TryGetPointAt(payload.targetQ, payload.targetR, out target)) {
				continue;
			}

			if (target.eventKind != HexEventKind.Teleport) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Warning,
					"TELEPORT_TARGET_NOT_TELEPORT",
					"双向传送目标格不是 Teleport 类型，无法回程。",
					point,
					true
				));
				continue;
			}

			TeleportEventPayload returnPayload =
				ParsePayload<TeleportEventPayload>(target.payload);
			if (returnPayload == null) {
				continue;
			}

			bool pointsBack = returnPayload.targetQ == point.q &&
				returnPayload.targetR == point.r;
			if (!pointsBack) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Warning,
					"TELEPORT_NO_RETURN",
					"双向传送未互指：(" + point.q + "," + point.r + ") → (" +
					target.q + "," + target.r + ")，但终点未指回起点。",
					point,
					true
				));
			}
		}

		var oneWayEndCoords = new List<(int q, int r)>();
		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.eventKind == HexEventKind.OneWayTeleportEnd) {
				oneWayEndCoords.Add((point.q, point.r));
			}
		}

		for (int e = 0; e < oneWayEndCoords.Count; e++) {
			int eq = oneWayEndCoords[e].q;
			int er = oneWayEndCoords[e].r;
			bool referenced = false;
			for (int i = 0; i < asset.points.Count; i++) {
				HexMapPointData point = asset.points[i];
				if (point.eventKind != HexEventKind.OneWayTeleportStart) {
					continue;
				}
				TeleportEventPayload payload = ParsePayload<TeleportEventPayload>(point.payload);
				if (payload != null && payload.targetQ == eq && payload.targetR == er) {
					referenced = true;
					break;
				}
			}
			if (!referenced) {
				report.Add(HexWorldMapValidationIssue.AtCoord(
					HexWorldMapValidationSeverity.Warning,
					"ONEWAY_END_UNREACHABLE",
					"单向传送终点未被任何起点引用。",
					eq,
					er
				));
			}
		}

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.eventKind != HexEventKind.OneWayTeleportStart) {
				continue;
			}
			TeleportEventPayload payload = ParsePayload<TeleportEventPayload>(point.payload);
			if (payload == null) {
				continue;
			}
			HexMapPointData target;
			if (!ctx.TryGetPointAt(payload.targetQ, payload.targetR, out target)) {
				continue;
			}
			if (target.eventKind != HexEventKind.OneWayTeleportEnd) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Warning,
					"ONEWAY_START_TARGET_KIND",
					"单向传送起点目标格不是 OneWayTeleportEnd 类型。",
					point,
					true
				));
			}
		}
	}

	static void ValidateConnectivity (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		if (!TryResolveTraversalRoot(asset, out int startId)) {
			return;
		}

		var reachable = new HashSet<int>();
		var queue = new Queue<int>();
		reachable.Add(startId);
		queue.Enqueue(startId);

		while (queue.Count > 0) {
			int currentId = queue.Dequeue();
			int index = ctx.IndexOfPointId(currentId);
			if (index < 0) {
				continue;
			}

			List<int> walkNeighbors = ctx.walkNeighborPointIds[index];
			for (int i = 0; i < walkNeighbors.Count; i++) {
				int neighborId = walkNeighbors[i];
				if (reachable.Add(neighborId)) {
					queue.Enqueue(neighborId);
				}
			}

			for (int t = 0; t < ctx.teleportEdges.Count; t++) {
				if (ctx.teleportEdges[t].fromPointId != currentId) {
					continue;
				}
				int toId = ctx.teleportEdges[t].toPointId;
				if (reachable.Add(toId)) {
					queue.Enqueue(toId);
				}
			}
		}

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (reachable.Contains(point.pointId)) {
				continue;
			}
			if (point.eventKind == HexEventKind.Final ||
				point.eventKind == HexEventKind.Start) {
				continue;
			}
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Warning,
				"GRAPH_ISLAND",
				"该点从起点不可达（孤立分支，步行+传送）。",
				point,
				true
			));
		}
	}

	static void ValidateStartAndFinal (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		HexWorldMapValidationReport report
	)
	{
		HexMapPointData startPoint = default;
		bool hasStartPoint = false;
		int startCount = 0;
		int finalCount = 0;

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.kind != HexPointKind.Event) {
				continue;
			}

			if (point.eventKind == HexEventKind.Start) {
				startCount++;
				startPoint = point;
				hasStartPoint = true;
			}
			else if (point.eventKind == HexEventKind.Final) {
				finalCount++;
			}
		}

		if (startCount == 0) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"START_MISSING",
				"地图必须包含恰好一个起点（Start 事件格，即出生点）。"
			));
		}
		else if (startCount > 1) {
			for (int i = 0; i < asset.points.Count; i++) {
				HexMapPointData point = asset.points[i];
				if (point.eventKind == HexEventKind.Start) {
					report.Add(HexWorldMapValidationIssue.Create(
						HexWorldMapValidationSeverity.Error,
						"START_MULTIPLE",
						"地图存在多个起点（Start），全图只能有一个。",
						point,
						true
					));
				}
			}
		}

		if (finalCount == 0) {
			report.Add(HexWorldMapValidationIssue.Create(
				HexWorldMapValidationSeverity.Error,
				"FINAL_MISSING",
				"地图至少需要一个终点（Final 事件格）。"
			));
		}

		if (!hasStartPoint || startCount != 1) {
			return;
		}

		asset.SyncPlayerStartFromStartPoint();
		var reachable = CollectReachablePointIds(asset, ctx, startPoint.pointId);

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.eventKind != HexEventKind.Final) {
				continue;
			}
			if (!reachable.Contains(point.pointId)) {
				report.Add(HexWorldMapValidationIssue.Create(
					HexWorldMapValidationSeverity.Error,
					"FINAL_UNREACHABLE",
					"该终点从起点不可达（步行+传送）。",
					point,
					true
				));
			}
		}
	}

	static bool TryResolveTraversalRoot (HexWorldMapAsset asset, out int rootPointId)
	{
		rootPointId = 0;
		int startCount = 0;
		HexMapPointData startPoint = default;

		for (int i = 0; i < asset.points.Count; i++) {
			HexMapPointData point = asset.points[i];
			if (point.eventKind != HexEventKind.Start) {
				continue;
			}
			startCount++;
			startPoint = point;
		}

		if (startCount == 1) {
			rootPointId = startPoint.pointId;
			return true;
		}

		return false;
	}

	static HashSet<int> CollectReachablePointIds (
		HexWorldMapAsset asset,
		HexWorldMapValidationContext ctx,
		int startPointId
	)
	{
		var reachable = new HashSet<int>();
		var queue = new Queue<int>();
		reachable.Add(startPointId);
		queue.Enqueue(startPointId);

		while (queue.Count > 0) {
			int currentId = queue.Dequeue();
			int index = ctx.IndexOfPointId(currentId);
			if (index < 0) {
				continue;
			}

			List<int> walkNeighbors = ctx.walkNeighborPointIds[index];
			for (int i = 0; i < walkNeighbors.Count; i++) {
				int neighborId = walkNeighbors[i];
				if (reachable.Add(neighborId)) {
					queue.Enqueue(neighborId);
				}
			}

			for (int t = 0; t < ctx.teleportEdges.Count; t++) {
				if (ctx.teleportEdges[t].fromPointId != currentId) {
					continue;
				}
				int toId = ctx.teleportEdges[t].toPointId;
				if (reachable.Add(toId)) {
					queue.Enqueue(toId);
				}
			}
		}

		return reachable;
	}

	static T ParsePayload<T> (string payload) where T : class
	{
		if (string.IsNullOrEmpty(payload)) {
			return null;
		}
		try {
			return JsonUtility.FromJson<T>(payload);
		}
		catch {
			return null;
		}
	}
}
