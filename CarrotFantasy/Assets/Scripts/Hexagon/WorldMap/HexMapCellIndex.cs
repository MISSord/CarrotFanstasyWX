/// <summary>
/// 大地图矩形边界内六边形格子的线性索引（与 width/height 对齐）。
/// </summary>
public static class HexMapCellIndex
{
	public static bool TryPack (int q, int r, int mapWidth, int mapHeight, out int cellIndex)
	{
		cellIndex = -1;
		int col = q + r / 2;
		int row = r;
		if (col < 0 || col >= mapWidth || row < 0 || row >= mapHeight) {
			return false;
		}
		cellIndex = row * mapWidth + col;
		return true;
	}

	public static bool TryUnpack (int cellIndex, int mapWidth, int mapHeight, out int q, out int r)
	{
		q = 0;
		r = 0;
		if (cellIndex < 0 || cellIndex >= mapWidth * mapHeight) {
			return false;
		}
		int row = cellIndex / mapWidth;
		int col = cellIndex % mapWidth;
		HexCoordinates coordinates = HexCoordinates.FromOffsetCoordinates(col, row);
		q = coordinates.X;
		r = coordinates.Z;
		return true;
	}
}
