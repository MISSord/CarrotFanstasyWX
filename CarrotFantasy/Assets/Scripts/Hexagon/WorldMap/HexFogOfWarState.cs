using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战争迷雾逻辑：格子一旦被揭开则永久保持（不会再次隐藏）。
/// </summary>
public sealed class HexFogOfWarState
{
	readonly int mapWidth;
	readonly int mapHeight;
	readonly int cellCount;
	readonly bool[] revealed;

	public int MapWidth {
		get { return mapWidth; }
	}

	public int MapHeight {
		get { return mapHeight; }
	}

	public HexFogOfWarState (int mapWidth, int mapHeight)
	{
		this.mapWidth = mapWidth;
		this.mapHeight = mapHeight;
		cellCount = mapWidth * mapHeight;
		revealed = new bool[cellCount];
	}

	public bool IsRevealed (int q, int r)
	{
		int cellIndex;
		if (!HexMapCellIndex.TryPack(q, r, mapWidth, mapHeight, out cellIndex)) {
			return false;
		}
		return revealed[cellIndex];
	}

	public bool IsRevealed (HexCoordinates coordinates)
	{
		return IsRevealed(coordinates.X, coordinates.Z);
	}

	/// <summary>玩家到达某格后：本格 + 按邻居状态揭开周围六格（已揭开格不会被重新隐藏）。</summary>
	public void RevealAroundPlayer (HexWorldMapRuntime map, HexCoordinates center)
	{
		if (map == null) {
			return;
		}

		RevealCell(center.X, center.Z);

		for (int d = 0; d < HexCoordinates.Directions.Length; d++) {
			HexCoordinates neighbor = center.GetNeighbor(d);
			if (!IsWithinMapBounds(neighbor)) {
				continue;
			}
			if (ShouldRevealNeighbor(map, neighbor)) {
				RevealCell(neighbor.X, neighbor.Z);
			}
		}
	}

	public void ImportFromProgress (HexWorldProgress progress)
	{
		if (progress == null || progress.revealedCellIndices == null) {
			return;
		}

		for (int i = 0; i < progress.revealedCellIndices.Count; i++) {
			int cellIndex = progress.revealedCellIndices[i];
			if (cellIndex >= 0 && cellIndex < cellCount) {
				revealed[cellIndex] = true;
			}
		}
	}

	public void ExportToProgress (HexWorldProgress progress)
	{
		if (progress == null) {
			return;
		}

		if (progress.revealedCellIndices == null) {
			progress.revealedCellIndices = new List<int>();
		}
		else {
			progress.revealedCellIndices.Clear();
		}

		for (int i = 0; i < cellCount; i++) {
			if (revealed[i]) {
				progress.revealedCellIndices.Add(i);
			}
		}
	}

	void RevealCell (int q, int r)
	{
		int cellIndex;
		if (!HexMapCellIndex.TryPack(q, r, mapWidth, mapHeight, out cellIndex)) {
			return;
		}
		revealed[cellIndex] = true;
	}

	bool IsWithinMapBounds (HexCoordinates coordinates)
	{
		int col = coordinates.X + coordinates.Z / 2;
		int row = coordinates.Z;
		return col >= 0 && col < mapWidth && row >= 0 && row < mapHeight;
	}

	static bool ShouldRevealNeighbor (HexWorldMapRuntime map, HexCoordinates neighbor)
	{
		HexMapPointRuntime point = map.GetPointAt(neighbor);
		if (point == null) {
			return true;
		}
		if (point.isBlocked) {
			return false;
		}
		return true;
	}
}
