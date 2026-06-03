using UnityEngine;

/// <summary>
/// 六边柱渲染用纯数据，与 MonoBehaviour 解耦。
/// </summary>
public struct HexCellRenderData
{
	public HexCoordinates coordinates;
	public Vector3 localPosition;
	public Color color;

	public static HexCellRenderData Create (HexCoordinates coordinates, Color color)
	{
		return new HexCellRenderData {
			coordinates = coordinates,
			localPosition = coordinates.ToLocalPosition(),
			color = color
		};
	}

	public static HexCellRenderData FromHexCell (HexCell cell)
	{
		if (cell == null) {
			return default;
		}

		return new HexCellRenderData {
			coordinates = cell.coordinates,
			localPosition = cell.transform.localPosition,
			color = cell.color
		};
	}

	public void SyncLocalPositionFromCoordinates ()
	{
		localPosition = coordinates.ToLocalPosition();
	}
}
