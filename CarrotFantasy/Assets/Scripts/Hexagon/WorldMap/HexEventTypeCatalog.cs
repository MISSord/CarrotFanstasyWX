using UnityEngine;

/// <summary>
/// 事件类型静态元数据表：显示名、地图色、触发时机与 PostTriggerRule（BlockSelf 等）。
/// </summary>
public struct HexEventTypeInfo
{
	public HexEventKind kind;
	public string displayName;
	public Color mapColor;
	public TriggerTiming triggerTiming;
	public PostTriggerRule postRule;
}

public static class HexEventTypeCatalog
{
	static readonly HexEventTypeInfo[] Table = {
		new HexEventTypeInfo {
			kind = HexEventKind.Start,
			displayName = "Start 起点",
			mapColor = new Color(0.25f, 0.95f, 0.4f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Final,
			displayName = "Final",
			mapColor = new Color(0.95f, 0.25f, 0.75f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Battle,
			displayName = "Battle",
			mapColor = new Color(0.9f, 0.3f, 0.3f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Shop,
			displayName = "Shop",
			mapColor = new Color(0.3f, 0.5f, 0.95f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Teleport,
			displayName = "Teleport 2-Way",
			mapColor = new Color(0.65f, 0.35f, 0.95f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Fragile,
			displayName = "Fragile",
			mapColor = new Color(0.85f, 0.55f, 0.2f),
			triggerTiming = TriggerTiming.OnLeave,
			postRule = PostTriggerRule.BlockSelf
		},
		new HexEventTypeInfo {
			kind = HexEventKind.Random,
			displayName = "Random",
			mapColor = new Color(0.95f, 0.85f, 0.2f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.OneWayTeleportStart,
			displayName = "One-Way Start",
			mapColor = new Color(0.25f, 0.9f, 0.95f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		},
		new HexEventTypeInfo {
			kind = HexEventKind.OneWayTeleportEnd,
			displayName = "One-Way End",
			mapColor = new Color(0.15f, 0.5f, 0.6f),
			triggerTiming = TriggerTiming.OnEnter,
			postRule = PostTriggerRule.None
		}
	};

	static readonly HexEventKind[] PaintableKinds = {
		HexEventKind.Start,
		HexEventKind.Final,
		HexEventKind.Battle,
		HexEventKind.Shop,
		HexEventKind.Teleport,
		HexEventKind.OneWayTeleportStart,
		HexEventKind.OneWayTeleportEnd,
		HexEventKind.Fragile,
		HexEventKind.Random
	};

	public static HexEventKind[] GetPaintableKinds ()
	{
		return PaintableKinds;
	}

	public static bool TryGet (HexEventKind kind, out HexEventTypeInfo info)
	{
		for (int i = 0; i < Table.Length; i++) {
			if (Table[i].kind == kind) {
				info = Table[i];
				return true;
			}
		}
		info = default;
		return false;
	}

	public static TriggerTiming ResolveTriggerTiming (
		HexEventKind kind,
		TriggerTiming pointFallback
	)
	{
		if (TryGet(kind, out HexEventTypeInfo info)) {
			return info.triggerTiming;
		}
		return pointFallback;
	}

	public static PostTriggerRule ResolvePostRule (
		HexEventKind kind,
		PostTriggerRule pointFallback
	)
	{
		if (TryGet(kind, out HexEventTypeInfo info)) {
			return info.postRule;
		}
		return pointFallback;
	}

	public static Color ResolveMapColor (HexEventKind kind, Color fallback)
	{
		if (TryGet(kind, out HexEventTypeInfo info)) {
			return info.mapColor;
		}
		return fallback;
	}

	public static string GetDisplayName (HexEventKind kind)
	{
		if (TryGet(kind, out HexEventTypeInfo info)) {
			return info.displayName;
		}
		return kind.ToString();
	}
}
