using UnityEngine;

/// <summary>
/// 六边形几何与布局间距。由 HexMapLayout 在 Awake / OnValidate 时注入。
/// </summary>
public static class HexMetrics {

	public static float OuterRadius { get; private set; } = 10f;

	public static float CellGap { get; private set; } = 0f;

	public static float InnerRadius {
		get { return OuterRadius * 0.866025404f; }
	}

	/// <summary>相邻格子中心水平间距（含 cellGap）。</summary>
	public static float HorizontalSpacing {
		get { return InnerRadius * 2f + CellGap; }
	}

	/// <summary>相邻行中心垂直间距（含 cellGap）。</summary>
	public static float VerticalSpacing {
		get { return OuterRadius * 1.5f + CellGap; }
	}

	static Vector3[] corners;

	public static Vector3[] Corners {
		get {
			if (corners == null) {
				RebuildCorners();
			}
			return corners;
		}
	}

	public static void Apply (float outerRadius, float cellGap)
	{
		OuterRadius = Mathf.Max(0.01f, outerRadius);
		CellGap = Mathf.Max(0f, cellGap);
		RebuildCorners();
	}

	static void RebuildCorners ()
	{
		float outer = OuterRadius;
		float inner = InnerRadius;
		corners = new Vector3[] {
			new Vector3(0f, 0f, outer),
			new Vector3(inner, 0f, 0.5f * outer),
			new Vector3(inner, 0f, -0.5f * outer),
			new Vector3(0f, 0f, -outer),
			new Vector3(-inner, 0f, -0.5f * outer),
			new Vector3(-inner, 0f, 0.5f * outer),
			new Vector3(0f, 0f, outer)
		};
	}
}
