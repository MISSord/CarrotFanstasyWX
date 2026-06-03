using UnityEngine;

/// <summary>
/// 单个点位的运行时状态，由 HexMapPointData 加载并可在游戏中变化。
/// <para>触发去重与行走性分离：</para>
/// <list type="bullet">
/// <item><see cref="enterConsumed"/> — 仅挡 <see cref="TriggerTiming.OnEnter"/> 重复触发，不封格。</item>
/// <item><see cref="leaveHandled"/> — 仅挡 <see cref="TriggerTiming.OnLeave"/> 重复触发。</item>
/// <item><see cref="isBlocked"/> — 不可再作为移动目标（易碎等 <see cref="PostTriggerRule.BlockSelf"/>）。</item>
/// </list>
/// </summary>
public class HexMapPointRuntime
{
	public HexMapPointData data;

	/// <summary>
	/// 该格不可再走入（<see cref="PostTriggerRule.BlockSelf"/>，如易碎离开后的碎格）。
	/// 与 <see cref="enterConsumed"/> 无关：战斗胜利只设 enterConsumed，不设 isBlocked。
	/// </summary>
	public bool isBlocked;

	/// <summary>
	/// 本格 OnLeave 逻辑已执行完毕（离开时在 TryTriggerLeave 内自动置位）。
	/// 易碎点另会通过 BlockSelf 设置 <see cref="isBlocked"/>。
	/// </summary>
	public bool leaveHandled;

	/// <summary>
	/// 本格 OnEnter 逻辑已视为完成，TryTriggerEnter 将跳过（如战斗胜利后由 OnBattleWon 置位）。
	/// 不表示封格；商店/传送等默认不会自动置位，每次踏入仍会触发。
	/// </summary>
	public bool enterConsumed;

	public HexCoordinates Coordinates {
		get { return data.Coordinates; }
	}

	/// <summary>作为移动目标时是否可走（blocked 的路径点和事件点均不可进入）。</summary>
	public bool IsWalkableTarget {
		get {
			if (isBlocked) {
				return false;
			}
			if (data.kind == HexPointKind.Path) {
				return true;
			}
			return data.kind == HexPointKind.Event;
		}
	}

	public T GetPayload<T> () where T : class
	{
		if (string.IsNullOrEmpty(data.payload)) {
			return null;
		}
		return JsonUtility.FromJson<T>(data.payload);
	}

	public TriggerTiming ResolveTriggerTiming ()
	{
		if (data.kind != HexPointKind.Event) {
			return TriggerTiming.OnEnter;
		}
		return HexEventTypeCatalog.ResolveTriggerTiming(data.eventKind, data.triggerTiming);
	}

	public PostTriggerRule ResolvePostRule ()
	{
		if (data.kind != HexPointKind.Event) {
			return PostTriggerRule.None;
		}
		return HexEventTypeCatalog.ResolvePostRule(data.eventKind, data.postRule);
	}
}
