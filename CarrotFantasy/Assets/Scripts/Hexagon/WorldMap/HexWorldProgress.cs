using System;
using System.Collections.Generic;

/// <summary>
/// 玩家在大地图上的进度存档，与 HexWorldMapAsset 静态布局分离。
/// 对应运行时 <see cref="HexMapPointRuntime"/> 的 isBlocked / enterConsumed / leaveHandled。
/// </summary>
[Serializable]
public class HexWorldProgress
{
	public int mapId;
	public int currentPointId;

	/// <summary>isBlocked：不可再走入（主要为易碎 BlockSelf，不含战斗胜利）。</summary>
	public List<int> blockedPointIds = new List<int>();

	/// <summary>enterConsumed：OnEnter 已处理，可路过但不再触发进入逻辑（如战斗已胜）。</summary>
	public List<int> consumedEnterPointIds = new List<int>();

	/// <summary>leaveHandled：OnLeave 已处理（如易碎已触发离开逻辑）。</summary>
	public List<int> leaveHandledPointIds = new List<int>();

	/// <summary>战争迷雾已永久揭开的格子索引（row * mapWidth + col），揭开后再也不会隐藏。</summary>
	public List<int> revealedCellIndices = new List<int>();
}
