using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大地图可视化：以纯数据驱动 HexMesh，仅为有效点位生成六边柱与标签。
/// </summary>
[RequireComponent(typeof(HexMapLayout))]
public class HexWorldMapView : MonoBehaviour
{
	public Text cellLabelPrefab;
	public Canvas labelCanvas;

	public Color pathColor = new Color(0.35f, 0.75f, 0.35f);
	public Color eventColor = new Color(0.95f, 0.85f, 0.2f);
	public Color blockedColor = new Color(0.35f, 0.35f, 0.35f);
	public Color movableColor = new Color(0.3f, 0.95f, 0.95f);
	public Color playerColor = new Color(0.95f, 0.35f, 0.35f);

	readonly Dictionary<int, int> pointIdToCellIndex = new Dictionary<int, int>();
	readonly List<HexCellRenderData> cellDataList = new List<HexCellRenderData>();
	readonly List<Text> labels = new List<Text>();
	HexCellRenderData[] cellDataCache;
	HexWorldMapRuntime runtime;
	HexMapLayout mapLayout;

	void Awake ()
	{
		mapLayout = GetComponent<HexMapLayout>();
		if (mapLayout != null) {
			mapLayout.Apply();
		}
	}

	void OnEnable ()
	{
		if (mapLayout == null) {
			mapLayout = GetComponent<HexMapLayout>();
		}
		if (mapLayout != null) {
			mapLayout.LayoutChanged += HandleLayoutChanged;
		}
	}

	void OnDisable ()
	{
		if (mapLayout != null) {
			mapLayout.LayoutChanged -= HandleLayoutChanged;
		}
	}

	void HandleLayoutChanged ()
	{
		RefreshLayout();
	}

	public void Build (HexWorldMapRuntime mapRuntime)
	{
		Clear();
		runtime = mapRuntime;

		foreach (HexMapPointRuntime point in runtime.AllPoints) {
			int index = cellDataList.Count;
			pointIdToCellIndex[point.data.pointId] = index;
			cellDataList.Add(HexCellRenderData.Create(
				point.Coordinates,
				GetPointColor(point)
			));

			if (cellLabelPrefab != null && labelCanvas != null) {
				Text label = Instantiate(cellLabelPrefab, labelCanvas.transform, false);
				Vector3 localPosition = point.Coordinates.ToLocalPosition();
				label.rectTransform.anchoredPosition = new Vector2(localPosition.x, localPosition.z);
				label.text = point.Coordinates.ToStringOnSeparateLines();
				labels.Add(label);
			}
		}

		RebuildCellDataCache();
	}

	public HexCellRenderData[] GetCellData ()
	{
		return cellDataCache;
	}

	void RebuildCellDataCache ()
	{
		cellDataCache = cellDataList.ToArray();
	}

	/// <summary>仅颜色变化时调用，返回 true 表示调用方应 RefreshColors。</summary>
	public bool RefreshColorsOnly ()
	{
		if (runtime == null) {
			return false;
		}

		if (cellDataCache == null || cellDataCache.Length != cellDataList.Count) {
			RebuildCellDataCache();
		}

		List<int> movable = runtime.GetMovableNeighborIds();
		foreach (KeyValuePair<int, int> pair in pointIdToCellIndex) {
			HexMapPointRuntime point = runtime.GetPoint(pair.Key);
			int index = pair.Value;
			HexCellRenderData data = cellDataList[index];
			data.color = GetPointColor(point, movable);
			cellDataList[index] = data;
			cellDataCache[index] = data;
		}
		return cellDataCache != null && cellDataCache.Length > 0;
	}

	public void Refresh ()
	{
		RefreshColorsOnly();
	}

	/// <summary>几何布局变化后调用，返回 true 表示需要更新 Mesh 顶点。</summary>
	public bool RefreshLayoutAndColors ()
	{
		if (cellDataCache == null || cellDataCache.Length != cellDataList.Count) {
			RebuildCellDataCache();
		}

		for (int i = 0; i < cellDataList.Count; i++) {
			HexCellRenderData data = cellDataList[i];
			data.SyncLocalPositionFromCoordinates();
			cellDataList[i] = data;
			cellDataCache[i] = data;
		}

		int labelIndex = 0;
		foreach (KeyValuePair<int, int> pair in pointIdToCellIndex) {
			if (labelIndex >= labels.Count) {
				break;
			}
			HexCellRenderData data = cellDataList[pair.Value];
			labels[labelIndex].rectTransform.anchoredPosition =
				new Vector2(data.localPosition.x, data.localPosition.z);
			labelIndex++;
		}

		if (runtime != null) {
			List<int> movable = runtime.GetMovableNeighborIds();
			foreach (KeyValuePair<int, int> pair in pointIdToCellIndex) {
				HexMapPointRuntime point = runtime.GetPoint(pair.Key);
				int index = pair.Value;
				HexCellRenderData data = cellDataList[index];
				data.color = GetPointColor(point, movable);
				cellDataList[index] = data;
				cellDataCache[index] = data;
			}
		}

		return cellDataCache != null && cellDataCache.Length > 0;
	}

	/// <summary>outerRadius / cellGap 变化后，更新格子位置与标签。</summary>
	public void RefreshLayout ()
	{
		RefreshLayoutAndColors();
	}

	/// <summary>射线命中 Mesh 后，将世界坐标还原为最近有效点 id。</summary>
	public bool TryGetPointIdFromPosition (Vector3 worldPosition, out int pointId)
	{
		pointId = 0;
		if (runtime == null) {
			return false;
		}

		Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
		HexCoordinates coordinates = HexCoordinates.FromPosition(localPosition);
		return runtime.TryGetPointIdAt(coordinates, out pointId);
	}

	Color GetPointColor (HexMapPointRuntime point)
	{
		return GetPointColor(point, null);
	}

	Color GetPointColor (HexMapPointRuntime point, List<int> movableNeighborIds)
	{
		if (point == null) {
			return blockedColor;
		}
		if (point.data.pointId == runtime.CurrentPointId) {
			return playerColor;
		}
		if (movableNeighborIds != null && movableNeighborIds.Contains(point.data.pointId)) {
			return movableColor;
		}
		if (point.isBlocked) {
			return blockedColor;
		}
		if (point.data.kind == HexPointKind.Path) {
			return pathColor;
		}
		return HexEventTypeCatalog.ResolveMapColor(point.data.eventKind, eventColor);
	}

	void Clear ()
	{
		for (int i = 0; i < labels.Count; i++) {
			if (labels[i] != null) {
				Destroy(labels[i].gameObject);
			}
		}
		labels.Clear();
		pointIdToCellIndex.Clear();
		cellDataList.Clear();
		cellDataCache = null;
	}
}
