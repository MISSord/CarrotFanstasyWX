using UnityEngine;

/// <summary>
/// 单位六边柱（中心在原点）的固定拓扑模板，供 HexMesh 复用。
/// </summary>
static class HexPrismTemplate
{
	const float Thickness = 2f;

	public static readonly Vector3[] Vertices;
	public static readonly Vector3[] Normals;
	public static readonly int[] Triangles;
	public static readonly int VerticesPerCell;
	public static readonly int TriangleIndicesPerCell;

	static HexPrismTemplate ()
	{
		var vertices = new System.Collections.Generic.List<Vector3>();
		var triangles = new System.Collections.Generic.List<int>();

		Vector3 topCenter = Vector3.zero;
		float bottomY = -Thickness;
		Vector3 bottomCenter = new Vector3(0f, bottomY, 0f);

		for (int i = 0; i < 6; i++) {
			AddTriangle(
				vertices,
				triangles,
				topCenter,
				TopCorner(topCenter, i),
				TopCorner(topCenter, i + 1)
			);
		}

		for (int i = 0; i < 6; i++) {
			AddTriangle(
				vertices,
				triangles,
				bottomCenter,
				BottomCorner(bottomY, i + 1),
				BottomCorner(bottomY, i)
			);
		}

		for (int i = 0; i < 6; i++) {
			Vector3 topA = TopCorner(topCenter, i);
			Vector3 topB = TopCorner(topCenter, i + 1);
			Vector3 bottomA = BottomCorner(bottomY, i);
			Vector3 bottomB = BottomCorner(bottomY, i + 1);

			AddTriangle(vertices, triangles, topA, bottomA, bottomB);
			AddTriangle(vertices, triangles, topA, bottomB, topB);
		}

		Vertices = vertices.ToArray();
		Triangles = triangles.ToArray();
		VerticesPerCell = Vertices.Length;
		TriangleIndicesPerCell = Triangles.Length;

		var mesh = new Mesh();
		mesh.SetVertices(Vertices);
		mesh.SetTriangles(Triangles, 0);
		mesh.RecalculateNormals();
		Normals = mesh.normals;
	}

	static Vector3 TopCorner (Vector3 center, int cornerIndex)
	{
		Vector3 corner = HexMetrics.Corners[cornerIndex];
		return new Vector3(center.x + corner.x, center.y, center.z + corner.z);
	}

	static Vector3 BottomCorner (float bottomY, int cornerIndex)
	{
		Vector3 corner = HexMetrics.Corners[cornerIndex];
		return new Vector3(corner.x, bottomY, corner.z);
	}

	static void AddTriangle (
		System.Collections.Generic.List<Vector3> vertices,
		System.Collections.Generic.List<int> triangles,
		Vector3 v1,
		Vector3 v2,
		Vector3 v3
	)
	{
		int vertexIndex = vertices.Count;
		vertices.Add(v1);
		vertices.Add(v2);
		vertices.Add(v3);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
	}
}
