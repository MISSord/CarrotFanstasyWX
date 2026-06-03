using UnityEngine;

/// <summary>
/// 按 HexEventKind 分发固定事件逻辑；每点差异由 payload 提供。
/// 去重由 HexWorldMapRuntime 负责：OnEnter 看 enterConsumed，OnLeave 看 leaveHandled。
/// </summary>
public static class HexEventDispatcher
{
	/// <summary>踏入时调用（战斗/商店/传送等）。是否再次触发取决于 enterConsumed。</summary>
	public static void OnEnter (HexMapContext context, HexMapPointRuntime point)
	{
		switch (point.data.eventKind) {
		case HexEventKind.Battle:
			BattleEventPayload battle = point.GetPayload<BattleEventPayload>();
			int encounterId = battle != null ? battle.encounterId : 0;
			context.RequestBattle(point.data.pointId, encounterId);
			break;
		case HexEventKind.Shop:
			context.RequestShop(point.data.pointId);
			break;
		case HexEventKind.Teleport:
			TeleportEventPayload teleport = point.GetPayload<TeleportEventPayload>();
			if (teleport == null) {
				Debug.LogWarning("Teleport point missing payload: " + point.data.pointId);
				return;
			}
			context.RequestTeleport(
				point.data.pointId,
				teleport.ResolveTargetPointId(context.Map.MapAsset.width)
			);
			break;
		case HexEventKind.OneWayTeleportStart:
			TeleportEventPayload oneWayStart = point.GetPayload<TeleportEventPayload>();
			if (oneWayStart == null) {
				Debug.LogWarning("One-way teleport start missing payload: " + point.data.pointId);
				return;
			}
			context.RequestOneWayTeleport(
				point.data.pointId,
				oneWayStart.ResolveTargetPointId(context.Map.MapAsset.width)
			);
			break;
		case HexEventKind.OneWayTeleportEnd:
			break;
		case HexEventKind.Start:
			break;
		case HexEventKind.Final:
			Debug.Log("HexEventDispatcher: reached final point " + point.data.pointId);
			break;
		case HexEventKind.Random:
			RandomEventPayload random = point.GetPayload<RandomEventPayload>();
			int randomEventId = random != null ? random.randomEventId : 0;
			context.RequestRandomEvent(point.data.pointId, randomEventId);
			break;
		}
	}

	/// <summary>离开时调用（易碎等）。调用方会在之后置 leaveHandled 并可能 BlockSelf。</summary>
	public static void OnLeave (HexMapContext context, HexMapPointRuntime point)
	{
		if (point.data.eventKind == HexEventKind.Fragile) {
			context.NotifyFragileBroken(point.data.pointId);
		}
	}
}
