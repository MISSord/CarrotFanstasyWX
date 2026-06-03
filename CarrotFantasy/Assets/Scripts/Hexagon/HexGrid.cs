using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HexMapLayout))]
public class HexGrid : MonoBehaviour {

	public int width = 6;
	public int height = 6;

	public Color defaultColor = Color.white;

	public HexCell cellPrefab;
	public Text cellLabelPrefab;

	HexCell[] cells;
	Text[] cellLabels;

	Canvas gridCanvas;
	HexMesh hexMesh;
	HexMapLayout mapLayout;

	void Awake () {
		mapLayout = GetComponent<HexMapLayout>();
		mapLayout.Apply();

		gridCanvas = GetComponentInChildren<Canvas>();
		hexMesh = GetComponentInChildren<HexMesh>();

		cells = new HexCell[height * width];
		cellLabels = new Text[height * width];

		for (int z = 0, i = 0; z < height; z++) {
			for (int x = 0; x < width; x++) {
				CreateCell(x, z, i++);
			}
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

	void Start () {
		hexMesh.Rebuild(SyncRenderDataCache(cells));
	}

	void HandleLayoutChanged ()
	{
		RefreshLayout();
	}

	public void RefreshLayout ()
	{
		if (cells == null) {
			return;
		}

		for (int i = 0; i < cells.Length; i++) {
			if (cells[i] == null) {
				continue;
			}
			Vector3 position = cells[i].coordinates.ToLocalPosition();
			cells[i].transform.localPosition = position;
			if (cellLabels != null && cellLabels[i] != null) {
				cellLabels[i].rectTransform.anchoredPosition =
					new Vector2(position.x, position.z);
			}
		}

		if (hexMesh != null && cells.Length > 0) {
			HexCellRenderData[] data = SyncRenderDataCache(cells);
			hexMesh.RefreshPositions(data, true);
		}
	}

	HexCellRenderData[] renderDataCache;

	HexCellRenderData[] SyncRenderDataCache (HexCell[] cells)
	{
		if (renderDataCache == null || renderDataCache.Length != cells.Length) {
			renderDataCache = new HexCellRenderData[cells.Length];
		}

		for (int i = 0; i < cells.Length; i++) {
			renderDataCache[i] = HexCellRenderData.FromHexCell(cells[i]);
		}

		return renderDataCache;
	}

	public void ColorCell (Vector3 position, Color color) {
		position = transform.InverseTransformPoint(position);
		HexCoordinates coordinates = HexCoordinates.FromPosition(position);
		int index = coordinates.X + coordinates.Z * width + coordinates.Z / 2;
		HexCell cell = cells[index];
		cell.color = color;
		hexMesh.RefreshCellColor(cells, index);
	}

	void CreateCell (int x, int z, int i) {
		HexCoordinates coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
		Vector3 position = coordinates.ToLocalPosition();

		HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
		cell.transform.SetParent(transform, false);
		cell.transform.localPosition = position;
		cell.coordinates = coordinates;
		cell.color = defaultColor;

		Text label = Instantiate<Text>(cellLabelPrefab);
		label.rectTransform.SetParent(gridCanvas.transform, false);
		label.rectTransform.anchoredPosition =
			new Vector2(position.x, position.z);
		label.text = cell.coordinates.ToStringOnSeparateLines();
		cellLabels[i] = label;
	}
}
