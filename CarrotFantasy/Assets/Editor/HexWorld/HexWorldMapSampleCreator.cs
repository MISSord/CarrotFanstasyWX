using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器菜单：一键生成演示用大地图 SO。
/// 默认仅含起点（Start）、通道（Path）与终点（Final），不含其它事件点。
/// </summary>
public static class HexWorldMapSampleCreator
{
	const string RootFolder = "Assets/Game/HexWorld/Sample";
	const int MapWidth = 30;

	[MenuItem("Hex/Create Sample World Map Assets")]
	static void CreateSampleAssets ()
	{
		EnsureFolder("Assets/Game");
		EnsureFolder("Assets/Game/HexWorld");
		EnsureFolder(RootFolder);

		HexWorldMapAsset map = ScriptableObject.CreateInstance<HexWorldMapAsset>();
		map.width = MapWidth;
		map.height = 30;
		map.points = BuildSamplePoints();
		map.SyncAllPointIds();
		map.SyncPlayerStartFromStartPoint();
		AssetDatabase.CreateAsset(map, RootFolder + "/SampleWorldMap.asset");

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Sample hex world map asset created at " + RootFolder);
	}

	static System.Collections.Generic.List<HexMapPointData> BuildSamplePoints ()
	{
		var points = new System.Collections.Generic.List<HexMapPointData>();
		const int startQ = 2;
		const int rowR = 5;
		const int finalQ = 10;

		points.Add(Point(startQ, rowR, HexPointKind.Event, HexEventKind.Start, null,
			TriggerTiming.OnEnter, PostTriggerRule.None));

		for (int q = startQ + 1; q < finalQ; q++) {
			points.Add(Point(q, rowR, HexPointKind.Path, HexEventKind.None, null,
				TriggerTiming.OnEnter, PostTriggerRule.None));
		}

		points.Add(Point(finalQ, rowR, HexPointKind.Event, HexEventKind.Final, null,
			TriggerTiming.OnEnter, PostTriggerRule.None));

		return points;
	}

	static HexMapPointData Point (
		int q,
		int r,
		HexPointKind kind,
		HexEventKind eventKind,
		string payload,
		TriggerTiming triggerTiming,
		PostTriggerRule postRule
	) {
		HexMapPointData data = new HexMapPointData();
		data.q = q;
		data.r = r;
		data.kind = kind;
		data.eventKind = eventKind;
		data.payload = payload;
		data.triggerTiming = triggerTiming;
		data.postRule = postRule;
		data.SyncPointId(MapWidth);
		return data;
	}

	static void EnsureFolder (string path)
	{
		if (AssetDatabase.IsValidFolder(path)) {
			return;
		}
		string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
		string folderName = System.IO.Path.GetFileName(path);
		if (!AssetDatabase.IsValidFolder(parent)) {
			EnsureFolder(parent);
		}
		AssetDatabase.CreateFolder(parent, folderName);
	}
}
