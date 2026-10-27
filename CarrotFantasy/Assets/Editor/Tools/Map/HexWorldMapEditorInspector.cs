using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexWorldMapEditor))]
public class HexWorldMapEditorInspector : Editor
{
	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector();

		HexWorldMapEditor editor = (HexWorldMapEditor)target;
		GUILayout.Space(8f);

		if (GUILayout.Button("Rebuild View")) {
			editor.RebuildVisuals();
		}
		if (GUILayout.Button("Validate Map")) {
			editor.RunValidation(showDialog: true);
		}
		if (GUILayout.Button("Save To Asset")) {
			editor.SaveToAsset();
		}

		EditorGUILayout.HelpBox(
			"进入 Play 模式，使用场景左侧「Hex World Map Editor」窗口编辑地图。\n" +
			"颜色图例在该窗口内（可折叠「颜色图例」）。\n" +
			"保存前会自动校验；也可点 Validate Map 预检。\n" +
			"起点：用 Event 工具绘制 Start（全图唯一）。\n" +
			"左键：当前工具 | 右键：删除点位 | 1~4：切换工具\n" +
			"WASD / 左键拖动：平移镜头（需在 Main Camera 上挂 HexMapCameraPan）",
			MessageType.Info
		);
	}
}
