using System;

/// <summary>
/// 大地图与外部系统（UI、战斗、商店）的桥接层。
/// 事件 SO 通过 Context 发起请求；业务层完成后调用 Notify* / Confirm* 回调。
/// </summary>
public class HexMapContext
{
	public HexWorldMapRuntime Map { get; private set; }

	public event Action OnStateChanged;
	public event Action<int, int> OnBattleRequested;
	public event Action<int> OnShopRequested;
	public event Action<int, int> OnTeleportRequested;
	public event Action<int, int> OnOneWayTeleportRequested;
	public event Action<int, int> OnRandomEventRequested;
	public event Action<int> OnFragileBroken;
	public event Action<int, int> OnPlayerMoved;

	public HexMapContext (HexWorldMapRuntime map)
	{
		this.Map = map;
	}

	public void RequestBattle (int pointId, int encounterId)
	{
		Map.SetMovementLocked(true);
		if (OnBattleRequested != null) {
			OnBattleRequested(pointId, encounterId);
		}
	}

	public void RequestShop (int pointId)
	{
		Map.SetMovementLocked(true);
		if (OnShopRequested != null) {
			OnShopRequested(pointId);
		}
	}

	public void RequestTeleport (int fromPointId, int targetPointId)
	{
		Map.SetMovementLocked(true);
		if (OnTeleportRequested != null) {
			OnTeleportRequested(fromPointId, targetPointId);
		}
	}

	/// <summary>单向传送：仅起点触发，终点不可反向传送。</summary>
	public void RequestOneWayTeleport (int fromPointId, int targetPointId)
	{
		Map.SetMovementLocked(true);
		if (OnOneWayTeleportRequested != null) {
			OnOneWayTeleportRequested(fromPointId, targetPointId);
		}
	}

	public void RequestRandomEvent (int pointId, int randomEventId)
	{
		Map.SetMovementLocked(true);
		if (OnRandomEventRequested != null) {
			OnRandomEventRequested(pointId, randomEventId);
		}
	}

	public void NotifyFragileBroken (int pointId)
	{
		if (OnFragileBroken != null) {
			OnFragileBroken(pointId);
		}
	}

	/// <summary>战斗胜利：enterConsumed=true（不封格），解锁移动。</summary>
	public void NotifyBattleWon (int pointId)
	{
		Map.OnBattleWon(pointId);
	}

	/// <summary>战斗失败：不修改 enterConsumed / isBlocked，解锁移动后可再次挑战。</summary>
	public void NotifyBattleLost (int pointId)
	{
		Map.SetMovementLocked(false);
		Map.RaiseStateChanged();
	}

	public void NotifyShopClosed ()
	{
		Map.SetMovementLocked(false);
		Map.RaiseStateChanged();
	}

	public void NotifyRandomEventClosed ()
	{
		Map.SetMovementLocked(false);
		Map.RaiseStateChanged();
	}

	public void ConfirmTeleport (int fromPointId, int targetPointId, bool confirmed)
	{
		if (confirmed) {
			Map.TeleportTo(fromPointId, targetPointId);
		}
		else {
			Map.SetMovementLocked(false);
			Map.RaiseStateChanged();
		}
	}

	public void RaiseStateChanged ()
	{
		if (OnStateChanged != null) {
			OnStateChanged();
		}
	}

	public void RaisePlayerMoved (int fromPointId, int toPointId)
	{
		if (OnPlayerMoved != null) {
			OnPlayerMoved(fromPointId, toPointId);
		}
	}
}
