using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour {

	public static int VerticesPerCell => HexPrismTemplate.VerticesPerCell;

	Mesh hexMesh;
	Vector3[] vertexBuffer;
	Vector3[] normalBuffer;
	Color[] colorBuffer;
	int[] triangleBuffer;

	HexCellRenderData[] hexCellScratch;
	MeshCollider meshCollider;
	int builtCellCount;
	int builtTriangleIndexCount;

	void Awake ()
	{
		EnsureInitialized();
	}

	void OnEnable ()
	{
		EnsureInitialized();
	}

	void EnsureInitialized ()
	{
		if (hexMesh != null) {
			return;
		}

		MeshFilter filter = GetComponent<MeshFilter>();
		hexMesh = new Mesh();
		hexMesh.name = "Hex Mesh";
		hexMesh.MarkDynamic();
		filter.mesh = hexMesh;

		meshCollider = GetComponent<MeshCollider>();
		if (meshCollider == null && Application.isPlaying) {
			meshCollider = gameObject.AddComponent<MeshCollider>();
		}
	}

	/// <summary>结构变化时全量重建几何、法线与颜色。</summary>
	public void Rebuild (HexCellRenderData[] cells, bool updateCollider = true)
	{
		EnsureInitialized();

		if (cells == null || cells.Length == 0) {
			ClearMesh();
			return;
		}

		EnsureBuffers(cells.Length, true);
		builtCellCount = cells.Length;

		for (int i = 0; i < cells.Length; i++) {
			WriteCellGeometry(cells[i].localPosition, i);
			WriteCellColor(cells[i].color, i, colorBuffer);
		}

		ApplyMesh(updateCollider);
	}

	/// <summary>兼容旧 HexGrid：从 HexCell 全量重建。</summary>
	public void Triangulate (HexCell[] cells)
	{
		Triangulate(cells, true);
	}

	public void Triangulate (HexCell[] cells, bool updateCollider)
	{
		if (cells == null || cells.Length == 0) {
			Rebuild(null, updateCollider);
			return;
		}

		EnsureHexCellScratch(cells.Length);
		for (int i = 0; i < cells.Length; i++) {
			hexCellScratch[i] = cells[i] != null
				? HexCellRenderData.FromHexCell(cells[i])
				: default;
		}

		Rebuild(hexCellScratch, updateCollider);
	}

	/// <summary>仅刷新顶点色，不更新 Collider。</summary>
	public void RefreshColors (HexCellRenderData[] cells)
	{
		EnsureInitialized();

		if (cells == null || cells.Length == 0) {
			return;
		}

		if (builtCellCount != cells.Length || hexMesh.vertexCount == 0) {
			Rebuild(cells, true);
			return;
		}

		EnsureColorBuffer(cells.Length);
		WriteCellColors(cells, colorBuffer);
		hexMesh.SetColors(colorBuffer);
	}

	/// <summary>布局参数变化时仅更新顶点位置，可选更新 Collider。</summary>
	public void RefreshPositions (HexCellRenderData[] cells, bool updateCollider = true)
	{
		EnsureInitialized();

		if (cells == null || cells.Length == 0) {
			return;
		}

		if (builtCellCount != cells.Length || hexMesh.vertexCount == 0) {
			Rebuild(cells, updateCollider);
			return;
		}

		EnsureVertexBuffers(cells.Length);
		for (int i = 0; i < cells.Length; i++) {
			WriteCellGeometry(cells[i].localPosition, i);
		}

		hexMesh.vertices = vertexBuffer;
		if (updateCollider && meshCollider != null) {
			meshCollider.sharedMesh = hexMesh;
		}
	}

	public void RefreshCellColor (HexCell[] cells, int cellIndex)
	{
		if (cells == null || cellIndex < 0 || cellIndex >= cells.Length || cells[cellIndex] == null) {
			return;
		}

		RefreshCellColor(cellIndex, cells[cellIndex].color);
	}

	public void RefreshCellColor (int cellIndex, Color color)
	{
		EnsureInitialized();

		if (cellIndex < 0 || cellIndex >= builtCellCount || hexMesh.vertexCount == 0) {
			return;
		}

		EnsureColorBuffer(builtCellCount);
		if (hexMesh.colors != null && hexMesh.colors.Length == colorBuffer.Length) {
			hexMesh.colors.CopyTo(colorBuffer, 0);
		}

		WriteCellColor(color, cellIndex, colorBuffer);
		hexMesh.SetColors(colorBuffer);
	}

	void ClearMesh ()
	{
		builtCellCount = 0;
		builtTriangleIndexCount = 0;
		hexMesh.Clear();
		if (meshCollider != null) {
			meshCollider.sharedMesh = null;
		}
	}

	void EnsureHexCellScratch (int cellCount)
	{
		if (hexCellScratch == null || hexCellScratch.Length != cellCount) {
			hexCellScratch = new HexCellRenderData[cellCount];
		}
	}

	void EnsureBuffers (int cellCount, bool forceRebuildTriangles)
	{
		EnsureVertexBuffers(cellCount);
		EnsureColorBuffer(cellCount);
		RebuildTriangleBuffer(cellCount, forceRebuildTriangles);
	}

	void EnsureVertexBuffers (int cellCount)
	{
		int vertCount = cellCount * HexPrismTemplate.VerticesPerCell;
		if (vertexBuffer == null || vertexBuffer.Length != vertCount) {
			vertexBuffer = new Vector3[vertCount];
			normalBuffer = new Vector3[vertCount];
		}
	}

	void EnsureColorBuffer (int cellCount)
	{
		int vertCount = cellCount * HexPrismTemplate.VerticesPerCell;
		if (colorBuffer == null || colorBuffer.Length != vertCount) {
			colorBuffer = new Color[vertCount];
		}
	}

	void RebuildTriangleBuffer (int cellCount, bool forceRebuild)
	{
		int triCount = cellCount * HexPrismTemplate.TriangleIndicesPerCell;
		if (!forceRebuild &&
			triangleBuffer != null &&
			triangleBuffer.Length == triCount &&
			builtTriangleIndexCount == HexPrismTemplate.TriangleIndicesPerCell) {
			return;
		}

		triangleBuffer = new int[triCount];
		int[] template = HexPrismTemplate.Triangles;
		for (int c = 0; c < cellCount; c++) {
			int dst = c * HexPrismTemplate.TriangleIndicesPerCell;
			int offset = c * HexPrismTemplate.VerticesPerCell;
			for (int t = 0; t < HexPrismTemplate.TriangleIndicesPerCell; t++) {
				triangleBuffer[dst + t] = template[t] + offset;
			}
		}

		builtTriangleIndexCount = HexPrismTemplate.TriangleIndicesPerCell;
	}

	void WriteCellGeometry (Vector3 center, int cellIndex)
	{
		int vertStart = cellIndex * HexPrismTemplate.VerticesPerCell;
		Vector3[] templateVertices = HexPrismTemplate.Vertices;
		Vector3[] templateNormals = HexPrismTemplate.Normals;
		for (int v = 0; v < HexPrismTemplate.VerticesPerCell; v++) {
			vertexBuffer[vertStart + v] = templateVertices[v] + center;
			normalBuffer[vertStart + v] = templateNormals[v];
		}
	}

	void WriteCellColors (HexCellRenderData[] cells, Color[] buffer)
	{
		for (int i = 0; i < cells.Length; i++) {
			WriteCellColor(cells[i].color, i, buffer);
		}
	}

	static void WriteCellColor (Color color, int cellIndex, Color[] buffer)
	{
		int start = cellIndex * HexPrismTemplate.VerticesPerCell;
		int end = start + HexPrismTemplate.VerticesPerCell;
		for (int v = start; v < end; v++) {
			buffer[v] = color;
		}
	}

	void ApplyMesh (bool updateCollider)
	{
		hexMesh.Clear();
		hexMesh.SetVertices(vertexBuffer);
		hexMesh.SetNormals(normalBuffer);
		hexMesh.SetColors(colorBuffer);
		hexMesh.SetTriangles(triangleBuffer, 0);

		if (updateCollider && meshCollider != null) {
			meshCollider.sharedMesh = hexMesh;
		}
	}
}
