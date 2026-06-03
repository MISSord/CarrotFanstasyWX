using UnityEngine;

/// <summary>
/// 由 odd-r 轴向坐标与地图宽度生成稳定的点位 id（与 HexGrid 单元索引一致）。
/// </summary>
public static class HexMapPointId
{
	public static int Encode (int q, int r, int mapWidth)
	{
		return q + r * mapWidth + r / 2;
	}

	public static int Encode (HexCoordinates coordinates, int mapWidth)
	{
		return Encode(coordinates.X, coordinates.Z, mapWidth);
	}

	public static bool TryDecode (int pointId, int mapWidth, out int q, out int r)
	{
		if (mapWidth <= 0) {
			q = 0;
			r = 0;
			return false;
		}

		r = pointId / mapWidth;
		int col = pointId % mapWidth;
		q = col - r / 2;
		return true;
	}

	public static bool TryDecode (int pointId, int mapWidth, out HexCoordinates coordinates)
	{
		if (!TryDecode(pointId, mapWidth, out int q, out int r)) {
			coordinates = default;
			return false;
		}

		coordinates = new HexCoordinates(q, r);
		return true;
	}
}
