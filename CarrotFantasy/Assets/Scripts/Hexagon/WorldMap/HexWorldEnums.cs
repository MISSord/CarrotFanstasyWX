/// <summary>
/// 大地图点位相关枚举。
/// 空点不在数据中存储，缺省坐标即视为空点。
/// </summary>

/// <summary>可行走点位的大类：路径点（纯通道）或事件点（带触发逻辑）。</summary>
public enum HexPointKind
{
	Path = 0,
	Event = 1
}

/// <summary>
/// 事件点类型。行为由 HexEventDispatcher 固定实现，每点参数见 payload。
/// </summary>
public enum HexEventKind
{
	None = 0,
	/// <summary>
	/// 战斗
	/// </summary>
	Battle = 1,
	/// <summary>
	/// 商店
	/// </summary>
	Shop = 2,
	/// <summary>
	/// 双向传送（两端均可互相传送）。
	/// </summary>
	Teleport = 3,
	/// <summary>
	/// 易碎
	/// </summary>
	Fragile = 4,
	/// <summary>
	/// 随机事件
	/// </summary>
	Random = 5,
	/// <summary>
	/// 最终点
	/// </summary>
	Final = 6,
	/// <summary>
	/// 起点（玩家出生位置，全图唯一；与 playerStartQ/R 同步）。
	/// </summary>
	Start = 7,
	/// <summary>
	/// 单向传送起点（踏入后仅传送到指定终点）。
	/// </summary>
	OneWayTeleportStart = 8,
	/// <summary>
	/// 单向传送终点（仅作落点，踏入不触发传送）。
	/// </summary>
	OneWayTeleportEnd = 9,
}

/// <summary>
/// 事件触发时机。
/// OnEnter：踏入时触发（战斗、商店、传送）；
/// OnLeave：离开时触发（易碎点）。
/// </summary>
public enum TriggerTiming
{
	OnEnter = 0,
	OnLeave = 1
}

/// <summary>
/// 事件是否可重复触发（设计/配置用枚举，当前逻辑未统一读取）。
/// 实际运行时：OnEnter 用 enterConsumed（目前仅战斗胜利写入）；
/// OnLeave 用 leaveHandled + 可选 isBlocked。
/// </summary>
public enum HexEventRepeatPolicy
{
	Repeatable = 0,
	OneShot = 1
}

/// <summary>
/// 事件触发完成后的行走性变化（经 TryTriggerLeave → ApplyPostTrigger 应用）。
/// OnEnter 路径不调用 ApplyPostTrigger；战斗胜利也不走 BlockSelf。
/// BlockSelf：本格 isBlocked，常用于易碎（OnLeave）。
/// </summary>
public enum PostTriggerRule
{
	None = 0,
	BlockSelf = 1
}
