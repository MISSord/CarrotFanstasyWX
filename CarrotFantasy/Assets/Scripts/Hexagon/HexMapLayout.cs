using System;
using UnityEngine;

/// <summary>
/// 挂在地图根节点，Inspector 可调六边形大小与格子间距。
/// </summary>
[ExecuteAlways]
public class HexMapLayout : MonoBehaviour
{
	[SerializeField]
	[Min(0.01f)]
	[Tooltip("六边形外接圆半径，决定单个格子的显示大小。")]
	float outerRadius = 10f;

	[SerializeField]
	[Min(0f)]
	[Tooltip("格子中心之间的额外间距，0 为紧密拼接。")]
	float cellGap = 0f;

	public float OuterRadius {
		get { return outerRadius; }
	}

	public float CellGap {
		get { return cellGap; }
	}

	public event Action LayoutChanged;

	void OnEnable ()
	{
		ApplyAndNotify(false);
	}

	void OnValidate ()
	{
		ApplyAndNotify(true);
	}

	public void Apply ()
	{
		HexMetrics.Apply(outerRadius, cellGap);
	}

	void ApplyAndNotify (bool notifyListeners)
	{
		Apply();
		if (!notifyListeners) {
			return;
		}
		if (LayoutChanged != null) {
			LayoutChanged();
		}
		RefreshChildrenInEditMode();
	}

	void RefreshChildrenInEditMode ()
	{
		if (Application.isPlaying) {
			return;
		}

		HexGrid[] grids = GetComponentsInChildren<HexGrid>(true);
		for (int i = 0; i < grids.Length; i++) {
			grids[i].RefreshLayout();
		}

		HexWorldMapView[] views = GetComponentsInChildren<HexWorldMapView>(true);
		for (int i = 0; i < views.Length; i++) {
			views[i].RefreshLayout();
		}

		// HexWorldMapEditor 仅在 Play 模式下使用，Edit 模式不重建以免未初始化或重复 Instantiate
	}
}
