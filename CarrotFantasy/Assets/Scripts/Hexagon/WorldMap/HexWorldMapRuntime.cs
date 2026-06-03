using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大地图核心运行时：稀疏点位图、六邻接一步移动、Leave→Enter 触发链。
/// 玩家只能在 Path / 未 block 的 Event 点之间移动。
/// </summary>
public class HexWorldMapRuntime
{
	public HexWorldMapAsset MapAsset { get; private set; }
	public HexMapContext Context { get; private set; }

	readonly Dictionary<int, HexMapPointRuntime> pointById = new Dictionary<int, HexMapPointRuntime>();
	readonly Dictionary<(int q, int r), int> coordToPointId = new Dictionary<(int q, int r), int>();

	int currentPointId;

	/// <summary>战斗/商店/传送 UI 打开时为 true，禁止普通移动。</summary>
	bool movementLocked;

	public int CurrentPointId {
		get { return currentPointId; }
	}

	public bool MovementLocked {
		get { return movementLocked; }
	}

	public IEnumerable<HexMapPointRuntime> AllPoints {
		get { return pointById.Values; }
	}

	public void Load (
		HexWorldMapAsset mapAsset,
		HexWorldProgress progress = null
	) {
		MapAsset = mapAsset;
		mapAsset.SyncPlayerStartFromStartPoint();
		pointById.Clear();
		coordToPointId.Clear();

		for (int i = 0; i < mapAsset.points.Count; i++) {
			HexMapPointData pointData = mapAsset.points[i];
			int expectedId = HexMapPointId.Encode(pointData.q, pointData.r, mapAsset.width);
			if (pointData.pointId != 0 && pointData.pointId != expectedId) {
				Debug.LogWarning(
					"HexWorldMapRuntime: pointId mismatch at (" + pointData.q + ", " + pointData.r +
					"), stored=" + pointData.pointId + ", expected=" + expectedId +
					". Using coordinate-derived id."
				);
			}
			pointData.pointId = expectedId;

			HexMapPointRuntime runtime = new HexMapPointRuntime();
			runtime.data = pointData;
			pointById[pointData.pointId] = runtime;
			coordToPointId[(pointData.q, pointData.r)] = pointData.pointId;
		}

		if (progress != null) {
			ApplyProgress(progress);
			currentPointId = progress.currentPointId;
		}
		else if (!mapAsset.TryResolvePlayerStartPointId(out currentPointId)) {
			currentPointId = 0;
		}

		if (!pointById.ContainsKey(currentPointId)) {
			Debug.LogError("HexWorldMapRuntime: invalid start point id " + currentPointId);
		}

		Context = new HexMapContext(this);
		movementLocked = false;
	}

	public HexMapPointRuntime GetPoint (int pointId)
	{
		HexMapPointRuntime point;
		pointById.TryGetValue(pointId, out point);
		return point;
	}

	public HexMapPointRuntime GetPointAt (HexCoordinates coordinates)
	{
		int pointId;
		if (!coordToPointId.TryGetValue((coordinates.X, coordinates.Z), out pointId)) {
			return null;
		}
		return GetPoint(pointId);
	}

	public bool TryGetPointIdAt (HexCoordinates coordinates, out int pointId)
	{
		return coordToPointId.TryGetValue((coordinates.X, coordinates.Z), out pointId);
	}

	/// <summary>当前点六邻接内、且满足 CanMoveTo 的所有目标 pointId。</summary>
	public List<int> GetMovableNeighborIds ()
	{
		List<int> neighbors = new List<int>();
		HexMapPointRuntime current = GetPoint(currentPointId);
		if (current == null || movementLocked) {
			return neighbors;
		}

		for (int d = 0; d < HexCoordinates.Directions.Length; d++) {
			HexCoordinates neighborCoord = current.Coordinates.GetNeighbor(d);
			int neighborId;
			if (!TryGetPointIdAt(neighborCoord, out neighborId)) {
				continue;
			}
			if (CanMoveTo(currentPointId, neighborId)) {
				neighbors.Add(neighborId);
			}
		}
		return neighbors;
	}

	public bool CanMoveTo (int fromPointId, int toPointId)
	{
		HexMapPointRuntime from = GetPoint(fromPointId);
		HexMapPointRuntime to = GetPoint(toPointId);
		if (from == null || to == null) {
			return false;
		}
		// 只允许相邻一格，空点不在 coordToPointId 中，自然不可达
		if (HexCoordinates.GetDistance(from.Coordinates, to.Coordinates) != 1) {
			return false;
		}
		return to.IsWalkableTarget;
	}

	/// <summary>
	/// 普通移动：先更新位置，再 Leave(旧) → Enter(新)。
	/// 顺序保证从易碎格走出时，先碎旧格再触发新格 OnEnter。
	/// </summary>
	public bool TryMoveTo (int toPointId)
	{
		if (movementLocked) {
			return false;
		}
		if (!CanMoveTo(currentPointId, toPointId)) {
			return false;
		}

		int fromPointId = currentPointId;
		currentPointId = toPointId;

		TryTriggerLeave(fromPointId);
		TryTriggerEnter(toPointId);

		Context.RaisePlayerMoved(fromPointId, toPointId);
		RaiseStateChanged();
		return true;
	}

	/// <summary>传送：Leave 起点 → Enter 终点，逻辑同 TryMoveTo 但可跨多格。</summary>
	public void TeleportTo (int fromPointId, int targetPointId)
	{
		HexMapPointRuntime target = GetPoint(targetPointId);
		if (target == null || !target.IsWalkableTarget) {
			Debug.LogWarning("HexWorldMapRuntime: invalid teleport target " + targetPointId);
			movementLocked = false;
			RaiseStateChanged();
			return;
		}

		int previousCurrent = currentPointId;
		currentPointId = targetPointId;

		if (fromPointId != previousCurrent) {
			TryTriggerLeave(fromPointId);
		}
		else {
			TryTriggerLeave(previousCurrent);
		}
		TryTriggerEnter(targetPointId);

		Context.RaisePlayerMoved(previousCurrent, targetPointId);
		movementLocked = false;
		RaiseStateChanged();
	}

	/// <summary>战斗胜利：enterConsumed=true，格子仍可走过；失败勿调用。</summary>
	public void OnBattleWon (int pointId)
	{
		HexMapPointRuntime point = GetPoint(pointId);
		if (point != null) {
			point.enterConsumed = true;
		}
		movementLocked = false;
		RaiseStateChanged();
	}

	public void BlockPoint (int pointId)
	{
		HexMapPointRuntime point = GetPoint(pointId);
		if (point == null || point.isBlocked) {
			return;
		}
		point.isBlocked = true;
		RaiseStateChanged();
	}

	public void SetMovementLocked (bool locked)
	{
		movementLocked = locked;
	}

	public void RaiseStateChanged ()
	{
		Context.RaiseStateChanged();
	}

	public HexWorldProgress ExportProgress ()
	{
		HexWorldProgress progress = new HexWorldProgress();
		progress.currentPointId = currentPointId;
		foreach (KeyValuePair<int, HexMapPointRuntime> pair in pointById) {
			HexMapPointRuntime point = pair.Value;
			if (point.isBlocked) {
				progress.blockedPointIds.Add(point.data.pointId);
			}
			if (point.enterConsumed) {
				progress.consumedEnterPointIds.Add(point.data.pointId);
			}
			if (point.leaveHandled) {
				progress.leaveHandledPointIds.Add(point.data.pointId);
			}
		}
		return progress;
	}

	//加载进度
	void ApplyProgress (HexWorldProgress progress)
	{
		for (int i = 0; i < progress.blockedPointIds.Count; i++) {
			HexMapPointRuntime point = GetPoint(progress.blockedPointIds[i]);
			if (point != null) {
				point.isBlocked = true;
			}
		}
		for (int i = 0; i < progress.consumedEnterPointIds.Count; i++) {
			HexMapPointRuntime point = GetPoint(progress.consumedEnterPointIds[i]);
			if (point != null) {
				point.enterConsumed = true;
			}
		}
		for (int i = 0; i < progress.leaveHandledPointIds.Count; i++) {
			HexMapPointRuntime point = GetPoint(progress.leaveHandledPointIds[i]);
			if (point != null) {
				point.leaveHandled = true;
			}
		}
	}

	/// <summary>离开格子时触发；成功后置 leaveHandled，并按 postRule 可能 BlockSelf。</summary>
	void TryTriggerLeave (int pointId)
	{
		HexMapPointRuntime point = GetPoint(pointId);
		if (point == null || point.data.kind != HexPointKind.Event) {
			return;
		}
		if (point.leaveHandled) {
			return;
		}
		if (point.ResolveTriggerTiming() != TriggerTiming.OnLeave) {
			return;
		}

		HexEventDispatcher.OnLeave(Context, point);

		point.leaveHandled = true;
		ApplyPostTrigger(point);
	}

	/// <summary>踏入格子时触发；若 enterConsumed 则跳过。不会在此处置 enterConsumed。</summary>
	void TryTriggerEnter (int pointId)
	{
		HexMapPointRuntime point = GetPoint(pointId);
		if (point == null || point.data.kind != HexPointKind.Event) {
			return;
		}
		if (point.enterConsumed) {
			return;
		}
		if (point.ResolveTriggerTiming() != TriggerTiming.OnEnter) {
			return;
		}

		HexEventDispatcher.OnEnter(Context, point);
	}

	/// <summary>仅由 TryTriggerLeave 调用；OnEnter 不经过此处。BlockSelf → isBlocked。</summary>
	void ApplyPostTrigger (HexMapPointRuntime point)
	{
		if (point.ResolvePostRule() == PostTriggerRule.BlockSelf) {
			BlockPoint(point.data.pointId);
		}
	}
}
