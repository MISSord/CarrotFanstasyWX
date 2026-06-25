using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战争迷雾表现层：满网格 HexMesh 盖在地图上方，顶点 alpha 控制浓淡。
/// 已揭开格 alpha=0，未揭开为 fogColor。
/// </summary>
[RequireComponent(typeof(HexMapLayout))]
public class HexWorldMapFogView : MonoBehaviour
{
	public HexMesh fogHexMesh;

	[Header("Fog")]
	public float fogHeightOffset = 0.35f;
	public Color fogColor = new Color(0.04f, 0.04f, 0.07f, 0.92f);

	readonly List<HexCellRenderData> cellDataList = new List<HexCellRenderData>();
	HexCellRenderData[] cellDataCache;
	HexFogOfWarState fogState;
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

	public void Build (HexWorldMapAsset mapAsset, HexFogOfWarState state)
	{
		cellDataList.Clear();
		fogState = state;

		if (mapAsset == null || state == null) {
			cellDataCache = null;
			ClearFogMesh();
			return;
		}

		for (int row = 0; row < mapAsset.height; row++) {
			for (int col = 0; col < mapAsset.width; col++) {
				HexCoordinates coordinates = HexCoordinates.FromOffsetCoordinates(col, row);
				cellDataList.Add(CreateFogCell(coordinates));
			}
		}

		cellDataCache = cellDataList.ToArray();
		RebuildFogMesh();
		DisableFogCollider();
	}

	public void RefreshColors ()
	{
		if (fogState == null || cellDataCache == null || cellDataCache.Length == 0) {
			return;
		}

		for (int i = 0; i < cellDataList.Count; i++) {
			HexCellRenderData data = cellDataList[i];
			data.color = ResolveFogColor(data.coordinates);
			cellDataList[i] = data;
			cellDataCache[i] = data;
		}

		if (fogHexMesh != null) {
			fogHexMesh.RefreshColors(cellDataCache);
		}
	}

	public void RefreshLayout ()
	{
		if (cellDataCache == null || cellDataCache.Length == 0) {
			return;
		}

		for (int i = 0; i < cellDataList.Count; i++) {
			HexCellRenderData data = cellDataList[i];
			data.SyncLocalPositionFromCoordinates();
			data.localPosition += Vector3.up * fogHeightOffset;
			cellDataList[i] = data;
			cellDataCache[i] = data;
		}

		if (fogHexMesh != null) {
			fogHexMesh.RefreshPositions(cellDataCache, false);
			fogHexMesh.RefreshColors(cellDataCache);
		}
	}

	HexCellRenderData CreateFogCell (HexCoordinates coordinates)
	{
		HexCellRenderData data = HexCellRenderData.Create(coordinates, ResolveFogColor(coordinates));
		data.localPosition += Vector3.up * fogHeightOffset;
		return data;
	}

	Color ResolveFogColor (HexCoordinates coordinates)
	{
		if (fogState != null && fogState.IsRevealed(coordinates)) {
			return new Color(fogColor.r, fogColor.g, fogColor.b, 0f);
		}
		return fogColor;
	}

	void RebuildFogMesh ()
	{
		if (fogHexMesh == null || cellDataCache == null || cellDataCache.Length == 0) {
			return;
		}
		fogHexMesh.Rebuild(cellDataCache, false);
	}

	void ClearFogMesh ()
	{
		if (fogHexMesh != null) {
			fogHexMesh.Rebuild(null, false);
		}
	}

	void DisableFogCollider ()
	{
		if (fogHexMesh == null) {
			return;
		}
		MeshCollider collider = fogHexMesh.GetComponent<MeshCollider>();
		if (collider != null) {
			collider.enabled = false;
		}
	}
}
