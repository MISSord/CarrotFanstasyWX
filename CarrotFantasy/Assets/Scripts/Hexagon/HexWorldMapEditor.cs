using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 六边形大地图编辑器（编辑 HexWorldMapAsset 稀疏点位）。
/// 进入 Play 模式后，用左侧工具面板 + 场景点击编辑；编辑完成后点 Save 写回资源。
/// </summary>
[RequireComponent(typeof(HexMapLayout))]
public class HexWorldMapEditor : MonoBehaviour
{
	public enum EditorTool
	{
		PaintPath,
		PaintEvent,
		Erase,
		Select
	}

	public HexWorldMapAsset mapAsset;
	public HexMesh hexMesh;

	[Header("Display")]
	public bool showEmptyGrid = false;
	public Color emptyGridColor = new Color(0.12f, 0.12f, 0.14f, 1f);
	public Color pathColor = new Color(0.35f, 0.75f, 0.35f, 1f);
	public Color selectedColor = new Color(1f, 1f, 0.4f, 1f);

	[Header("New Event Defaults")]
	public int selectedEventTypeIndex;
	public int battleEncounterId = 1001;
	public int teleportTargetQ = 2;
	public int teleportTargetR = 5;
	public int randomEventId = 2001;

	EditorTool activeTool = EditorTool.Select;
	int selectedPointId = -1;
	bool isDirty;

	readonly List<HexCellRenderData> cellDataList = new List<HexCellRenderData>();
	HexCellRenderData[] cellDataCache;
	HexMapLayout mapLayout;
	HexMapCameraPan cameraPan;

	Rect toolPanelRect = new Rect(10f, 10f, 300f, 640f);
	bool showColorLegend = true;

	void Awake ()
	{
		mapLayout = GetComponent<HexMapLayout>();
		if (mapLayout != null) {
			mapLayout.Apply();
		}
		BindCameraPan();
	}

	void BindCameraPan ()
	{
		cameraPan = FindObjectOfType<HexMapCameraPan>();
		if (cameraPan != null) {
			cameraPan.SetInputBlocker(IsPointerOnToolPanel);
		}
	}

	void OnDestroy ()
	{
		if (cameraPan != null) {
			cameraPan.ClearInputBlocker();
		}
	}

	void OnEnable ()
	{
		activeTool = EditorTool.Select;
		if (mapLayout == null) {
			mapLayout = GetComponent<HexMapLayout>();
		}
		if (mapLayout != null) {
			mapLayout.LayoutChanged += HandleLayoutChangedForEditor;
		}
		RebuildVisuals();
	}

	void OnDisable ()
	{
		if (mapLayout != null) {
			mapLayout.LayoutChanged -= HandleLayoutChangedForEditor;
		}
	}

	void Update ()
	{
		if (mapAsset == null || hexMesh == null) {
			return;
		}

		if (Input.GetKeyDown(KeyCode.Alpha1)) {
			activeTool = EditorTool.PaintPath;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2)) {
			activeTool = EditorTool.PaintEvent;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3)) {
			activeTool = EditorTool.Erase;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4)) {
			activeTool = EditorTool.Select;
		}

		if (IsPointerOnToolPanel()) {
			return;
		}

		if (TryPickCoordinates(out HexCoordinates coordinates)) {
			if (Input.GetMouseButtonUp(0) &&
				(cameraPan == null || !cameraPan.ConsumeMapClick)) {
				ApplyTool(coordinates);
			}
			else if (Input.GetMouseButtonDown(1)) {
				RemovePointAt(coordinates);
			}
		}
	}

	void OnGUI ()
	{
		if (mapAsset == null) {
			return;
		}

		toolPanelRect = GUILayout.Window(
			GetInstanceID(),
			toolPanelRect,
			DrawToolPanel,
			"Hex World Map Editor"
		);
	}

	void DrawToolPanel (int windowId)
	{
		GUILayout.Label("Map: " + mapAsset.name);
		GUILayout.Label("Size: " + mapAsset.width + " x " + mapAsset.height);
		GUILayout.Label("Points: " + mapAsset.points.Count);
		if (mapAsset.TryGetStartPoint(out HexMapPointData startPoint)) {
			GUILayout.Label(
				"起点: (" + startPoint.q + ", " + startPoint.r + ")" +
				(isDirty ? " *" : "")
			);
		}
		else {
			GUILayout.Label("起点: 未设置（请用 Event 绘制 Start）" + (isDirty ? " *" : ""));
		}

		GUILayout.Space(6f);
		activeTool = (EditorTool)GUILayout.Toolbar((int)activeTool, new[] {
			"Path", "Event", "Erase", "Select"
		});

		GUILayout.Label("Shortcuts: 1~4 switch tools, RMB erase");

		if (activeTool == EditorTool.PaintEvent) {
			DrawEventPaintSettings();
		}

		DrawSelectedPointPanel();

		GUILayout.Space(8f);
		showEmptyGrid = GUILayout.Toggle(showEmptyGrid, "Show Empty Grid");

		DrawMapColorLegend();

		GUILayout.Space(8f);
		if (GUILayout.Button("Rebuild View")) {
			RebuildVisuals();
		}
		if (GUILayout.Button("Validate Map")) {
			RunValidation(showDialog: true);
		}
		if (GUILayout.Button("Save To Asset")) {
			SaveToAsset();
		}

		GUI.DragWindow();
	}

	void DrawMapColorLegend ()
	{
#if UNITY_EDITOR
		EditorGUILayout.Space(4f);
		showColorLegend = EditorGUILayout.Foldout(showColorLegend, "颜色图例", true);
		if (!showColorLegend) {
			return;
		}

		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		DrawLegendRow("路径 Path", pathColor);
		DrawLegendRow("选中 Select", selectedColor);
		if (showEmptyGrid) {
			DrawLegendRow("空网格 Empty", emptyGridColor);
		}

		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("事件格", EditorStyles.miniLabel);

		HexEventKind[] kinds = HexEventTypeCatalog.GetPaintableKinds();
		for (int i = 0; i < kinds.Length; i++) {
			if (!HexEventTypeCatalog.TryGet(kinds[i], out HexEventTypeInfo info)) {
				continue;
			}
			DrawLegendRow(info.displayName, info.mapColor);
		}

		EditorGUILayout.EndVertical();
#endif
	}

	void DrawLegendRow (string label, Color color)
	{
#if UNITY_EDITOR
		const float swatchWidth = 22f;
		const float swatchHeight = 14f;

		EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
		Rect swatchRect = GUILayoutUtility.GetRect(
			swatchWidth,
			swatchHeight,
			GUILayout.Width(swatchWidth),
			GUILayout.Height(swatchHeight)
		);

		if (Event.current.type == EventType.Repaint) {
			Color drawColor = color;
			drawColor.a = 1f;
			EditorGUI.DrawRect(swatchRect, drawColor);
			DrawLegendSwatchBorder(swatchRect);
		}

		EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));
		EditorGUILayout.EndHorizontal();
#endif
	}

#if UNITY_EDITOR
	static void DrawLegendSwatchBorder (Rect swatchRect)
	{
		Color border = EditorGUIUtility.isProSkin
			? new Color(1f, 1f, 1f, 0.45f)
			: new Color(0f, 0f, 0f, 0.5f);
		EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, swatchRect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.yMax - 1f, swatchRect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, 1f, swatchRect.height), border);
		EditorGUI.DrawRect(new Rect(swatchRect.xMax - 1f, swatchRect.y, 1f, swatchRect.height), border);
	}
#endif

	void DrawEventPaintSettings ()
	{
		GUILayout.Label("Paint Event Settings");
		HexEventKind[] kinds = HexEventTypeCatalog.GetPaintableKinds();
		if (kinds.Length == 0) {
			return;
		}

		string[] names = BuildEventTypeNames(kinds);
		selectedEventTypeIndex = DrawSelectionGrid(
			"Event Type",
			selectedEventTypeIndex,
			names
		);

		HexEventKind kind = kinds[Mathf.Clamp(selectedEventTypeIndex, 0, kinds.Length - 1)];
		switch (kind) {
		case HexEventKind.Battle:
			battleEncounterId = DrawIntField("Encounter Id", battleEncounterId);
			break;
		case HexEventKind.Teleport:
		case HexEventKind.OneWayTeleportStart:
			teleportTargetQ = DrawIntField("Target Q", teleportTargetQ);
			teleportTargetR = DrawIntField("Target R", teleportTargetR);
			break;
		case HexEventKind.OneWayTeleportEnd:
			GUILayout.Label("Destination only (no trigger).");
			break;
		case HexEventKind.Start:
			GUILayout.Label("全图唯一起点（即玩家出生位置）。");
			break;
		case HexEventKind.Final:
			GUILayout.Label("终点，可多个；保存时校验从起点可达。");
			break;
		case HexEventKind.Random:
			randomEventId = DrawIntField("Random Event Id", randomEventId);
			break;
		}
	}

	void DrawSelectedPointPanel ()
	{
		HexMapPointData? point = FindPoint(selectedPointId);
		if (point == null) {
			GUILayout.Label("Selected: none");
			return;
		}

		HexMapPointData data = point.Value;
		GUILayout.Label("Selected Point id=" + data.pointId);
		GUILayout.Label("Coord: (" + data.q + ", " + data.r + ")");
		GUILayout.Label("Kind: " + data.kind);

		if (data.kind == HexPointKind.Event) {
			GUILayout.Label("Event Kind: " + data.eventKind);
			GUILayout.Label("Payload: " + (string.IsNullOrEmpty(data.payload) ? "(empty)" : data.payload));

			switch (data.eventKind) {
			case HexEventKind.Battle:
				BattleEventPayload payload = ParsePayload<BattleEventPayload>(data.payload);
				int encounterId = payload != null ? payload.encounterId : battleEncounterId;
				int newEncounterId = DrawIntField("Encounter Id", encounterId);
				if (newEncounterId != encounterId) {
					SetPointPayload(data.pointId, JsonUtility.ToJson(
						new BattleEventPayload { encounterId = newEncounterId }
					));
				}
				break;
			case HexEventKind.Teleport:
			case HexEventKind.OneWayTeleportStart:
				DrawTeleportTargetPayloadEditor(data.pointId, data.payload);
				break;
			case HexEventKind.OneWayTeleportEnd:
				GUILayout.Label("One-way destination (walkable, no teleport trigger).");
				break;
			case HexEventKind.Start:
				GUILayout.Label("起点（出生位置）。");
				break;
			case HexEventKind.Final:
				GUILayout.Label("终点。");
				break;
			case HexEventKind.Random:
				RandomEventPayload random = ParsePayload<RandomEventPayload>(data.payload);
				int eventId = random != null ? random.randomEventId : randomEventId;
				int newEventId = DrawIntField("Random Event Id", eventId);
				if (newEventId != eventId) {
					SetPointPayload(data.pointId, JsonUtility.ToJson(
						new RandomEventPayload { randomEventId = newEventId }
					));
				}
				break;
			}
		}

		if (GUILayout.Button("Delete Selected Point")) {
			RemovePoint(data.pointId);
		}
	}

	string[] BuildEventTypeNames (HexEventKind[] kinds)
	{
		string[] names = new string[kinds.Length];
		for (int i = 0; i < kinds.Length; i++) {
			names[i] = (int)kinds[i] + " - " + HexEventTypeCatalog.GetDisplayName(kinds[i]);
		}
		return names;
	}

	int DrawIntField (string label, int value)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(120f));
		string text = GUILayout.TextField(value.ToString());
		GUILayout.EndHorizontal();
		return int.TryParse(text, out int parsed) ? parsed : value;
	}

	int DrawSelectionGrid (string label, int selectedIndex, string[] options)
	{
		GUILayout.Label(label);
		selectedIndex = GUILayout.SelectionGrid(
			Mathf.Clamp(selectedIndex, 0, options.Length - 1),
			options,
			1
		);
		return selectedIndex;
	}

	void DrawTeleportTargetPayloadEditor (int pointId, string payload)
	{
		TeleportEventPayload teleport = ParsePayload<TeleportEventPayload>(payload);
		int targetQ = teleport != null ? teleport.targetQ : teleportTargetQ;
		int targetR = teleport != null ? teleport.targetR : teleportTargetR;
		int newTargetQ = DrawIntField("Target Q", targetQ);
		int newTargetR = DrawIntField("Target R", targetR);
		if (newTargetQ != targetQ || newTargetR != targetR) {
			SetPointPayload(pointId, JsonUtility.ToJson(
				new TeleportEventPayload { targetQ = newTargetQ, targetR = newTargetR }
			));
		}
	}

	bool IsPointerOnToolPanel ()
	{
		Vector2 mouse = Input.mousePosition;
		mouse.y = Screen.height - mouse.y;
		return toolPanelRect.Contains(mouse);
	}

	bool TryPickCoordinates (out HexCoordinates coordinates)
	{
		coordinates = default;
		Camera camera = cameraPan != null ? cameraPan.TargetCamera : Camera.main;
		if (camera == null) {
			return false;
		}

		Ray ray = camera.ScreenPointToRay(Input.mousePosition);
		if (Physics.Raycast(ray, out RaycastHit hit)) {
			Vector3 local = transform.InverseTransformPoint(hit.point);
			coordinates = HexCoordinates.FromPosition(local);
			return IsWithinMapBounds(coordinates);
		}

		Plane plane = new Plane(Vector3.up, transform.position);
		if (plane.Raycast(ray, out float distance)) {
			Vector3 world = ray.GetPoint(distance);
			Vector3 local = transform.InverseTransformPoint(world);
			coordinates = HexCoordinates.FromPosition(local);
			return IsWithinMapBounds(coordinates);
		}

		return false;
	}

	bool IsWithinMapBounds (HexCoordinates coordinates)
	{
		int col = coordinates.X + coordinates.Z / 2;
		int row = coordinates.Z;
		return col >= 0 && col < mapAsset.width && row >= 0 && row < mapAsset.height;
	}

	void ApplyTool (HexCoordinates coordinates)
	{
		switch (activeTool) {
		case EditorTool.PaintPath:
			UpsertPathPoint(coordinates);
			break;
		case EditorTool.PaintEvent:
			UpsertEventPoint(coordinates);
			break;
		case EditorTool.Erase:
			RemovePointAt(coordinates);
			break;
		case EditorTool.Select:
			SelectPointAt(coordinates);
			break;
		}
	}

	void UpsertPathPoint (HexCoordinates coordinates)
	{
		if (TryFindPointIndex(coordinates, out int index)) {
			HexMapPointData data = mapAsset.points[index];
			data.kind = HexPointKind.Path;
			data.eventKind = HexEventKind.None;
			data.payload = null;
			data.triggerTiming = TriggerTiming.OnEnter;
			data.postRule = PostTriggerRule.None;
			data.SyncPointId(mapAsset.width);
			mapAsset.points[index] = data;
			selectedPointId = data.pointId;
		}
		else {
			HexMapPointData created = CreatePointData(
				coordinates,
				HexPointKind.Path,
				HexEventKind.None,
				null
			);
			mapAsset.points.Add(created);
			selectedPointId = created.pointId;
		}

		MarkDirtyAndRebuild();
	}

	void UpsertEventPoint (HexCoordinates coordinates)
	{
		HexEventKind[] kinds = HexEventTypeCatalog.GetPaintableKinds();
		if (kinds.Length == 0) {
			return;
		}

		selectedEventTypeIndex = Mathf.Clamp(
			selectedEventTypeIndex,
			0,
			kinds.Length - 1
		);
		HexEventKind kind = kinds[selectedEventTypeIndex];
		HexEventTypeCatalog.TryGet(kind, out HexEventTypeInfo info);

		string payload = BuildPayloadForEvent(kind);
		if (TryFindPointIndex(coordinates, out int index)) {
			HexMapPointData data = mapAsset.points[index];
			data.kind = HexPointKind.Event;
			data.eventKind = kind;
			data.payload = payload;
			data.triggerTiming = info.triggerTiming;
			data.postRule = info.postRule;
			data.SyncPointId(mapAsset.width);
			mapAsset.points[index] = data;
			selectedPointId = data.pointId;
		}
		else {
			HexMapPointData created = CreatePointData(
				coordinates,
				HexPointKind.Event,
				kind,
				payload
			);
			created.triggerTiming = info.triggerTiming;
			created.postRule = info.postRule;
			mapAsset.points.Add(created);
			selectedPointId = created.pointId;
		}

		if (kind == HexEventKind.Start) {
			mapAsset.playerStartQ = coordinates.X;
			mapAsset.playerStartR = coordinates.Z;
			DemoteOtherStartPoints(coordinates);
		}

		MarkDirtyAndRebuild();
	}

	void DemoteOtherStartPoints (HexCoordinates keepCoordinates)
	{
		for (int i = 0; i < mapAsset.points.Count; i++) {
			HexMapPointData point = mapAsset.points[i];
			if (point.eventKind != HexEventKind.Start) {
				continue;
			}
			if (point.q == keepCoordinates.X && point.r == keepCoordinates.Z) {
				continue;
			}

			point.kind = HexPointKind.Path;
			point.eventKind = HexEventKind.None;
			point.payload = null;
			point.triggerTiming = TriggerTiming.OnEnter;
			point.postRule = PostTriggerRule.None;
			mapAsset.points[i] = point;
		}
	}

	string BuildPayloadForEvent (HexEventKind kind)
	{
		switch (kind) {
		case HexEventKind.Battle:
			return JsonUtility.ToJson(new BattleEventPayload {
				encounterId = battleEncounterId
			});
		case HexEventKind.Teleport:
		case HexEventKind.OneWayTeleportStart:
			return JsonUtility.ToJson(new TeleportEventPayload {
				targetQ = teleportTargetQ,
				targetR = teleportTargetR
			});
		case HexEventKind.OneWayTeleportEnd:
		case HexEventKind.Start:
		case HexEventKind.Final:
			return null;
		case HexEventKind.Random:
			return JsonUtility.ToJson(new RandomEventPayload {
				randomEventId = randomEventId
			});
		default:
			return null;
		}
	}

	HexMapPointData CreatePointData (
		HexCoordinates coordinates,
		HexPointKind kind,
		HexEventKind eventKind,
		string payload
	) {
		HexMapPointData data = new HexMapPointData();
		data.q = coordinates.X;
		data.r = coordinates.Z;
		data.kind = kind;
		data.eventKind = eventKind;
		data.payload = payload;
		data.triggerTiming = TriggerTiming.OnEnter;
		data.postRule = PostTriggerRule.None;
		data.SyncPointId(mapAsset.width);
		return data;
	}

	void RemovePointAt (HexCoordinates coordinates)
	{
		if (!TryFindPointIndex(coordinates, out int index)) {
			return;
		}
		RemovePoint(mapAsset.points[index].pointId);
	}

	void RemovePoint (int pointId)
	{
		HexMapPointData removed = default;
		for (int i = mapAsset.points.Count - 1; i >= 0; i--) {
			if (mapAsset.points[i].pointId != pointId) {
				continue;
			}
			removed = mapAsset.points[i];
			mapAsset.points.RemoveAt(i);
			break;
		}

		if (selectedPointId == pointId) {
			selectedPointId = -1;
		}
		if (removed.eventKind == HexEventKind.Start) {
			mapAsset.playerStartQ = 0;
			mapAsset.playerStartR = 0;
		}

		MarkDirtyAndRebuild();
	}

	void SelectPointAt (HexCoordinates coordinates)
	{
		if (TryFindPointIndex(coordinates, out int index)) {
			selectedPointId = mapAsset.points[index].pointId;
		}
		else {
			selectedPointId = -1;
		}
		RefreshDisplayColors();
	}

	void SetPointPayload (int pointId, string payload)
	{
		for (int i = 0; i < mapAsset.points.Count; i++) {
			if (mapAsset.points[i].pointId != pointId) {
				continue;
			}
			HexMapPointData data = mapAsset.points[i];
			data.payload = payload;
			mapAsset.points[i] = data;
			break;
		}
		MarkDirtyAndRebuild();
	}

	bool TryFindPointIndex (HexCoordinates coordinates, out int index)
	{
		for (int i = 0; i < mapAsset.points.Count; i++) {
			HexMapPointData point = mapAsset.points[i];
			if (point.q == coordinates.X && point.r == coordinates.Z) {
				index = i;
				return true;
			}
		}
		index = -1;
		return false;
	}

	HexMapPointData? FindPoint (int pointId)
	{
		if (pointId <= 0) {
			return null;
		}
		for (int i = 0; i < mapAsset.points.Count; i++) {
			if (mapAsset.points[i].pointId == pointId) {
				return mapAsset.points[i];
			}
		}
		return null;
	}

	T ParsePayload<T> (string payload) where T : class
	{
		if (string.IsNullOrEmpty(payload)) {
			return null;
		}
		return JsonUtility.FromJson<T>(payload);
	}

	void MarkDirtyAndRebuild ()
	{
		isDirty = true;
		RebuildVisuals();
	}

	public void RebuildVisuals ()
	{
		if (mapAsset == null) {
			return;
		}

		mapAsset.SyncAllPointIds();

		if (hexMesh == null) {
			hexMesh = GetComponentInChildren<HexMesh>();
		}

		cellDataList.Clear();

		if (showEmptyGrid) {
			for (int z = 0; z < mapAsset.height; z++) {
				for (int x = 0; x < mapAsset.width; x++) {
					HexCoordinates coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
					AddCellData(coordinates, ResolveCellColor(coordinates));
				}
			}
		}
		else {
			for (int i = 0; i < mapAsset.points.Count; i++) {
				HexMapPointData point = mapAsset.points[i];
				AddCellData(point.Coordinates, ResolveCellColor(point.Coordinates));
			}
		}

		RebuildCellDataCache();
		if (hexMesh != null && cellDataCache != null && cellDataCache.Length > 0) {
			hexMesh.Rebuild(cellDataCache);
		}
		else if (hexMesh != null) {
			hexMesh.Rebuild(null);
		}
	}

	void HandleLayoutChangedForEditor ()
	{
		if (mapAsset == null || cellDataList.Count == 0) {
			RebuildVisuals();
			return;
		}

		if (hexMesh == null) {
			hexMesh = GetComponentInChildren<HexMesh>();
		}

		if (cellDataCache == null || cellDataCache.Length != cellDataList.Count) {
			RebuildCellDataCache();
		}

		for (int i = 0; i < cellDataList.Count; i++) {
			HexCellRenderData data = cellDataList[i];
			data.SyncLocalPositionFromCoordinates();
			data.color = ResolveCellColor(data.coordinates);
			cellDataList[i] = data;
			cellDataCache[i] = data;
		}

		if (hexMesh != null && cellDataCache != null && cellDataCache.Length > 0) {
			hexMesh.RefreshPositions(cellDataCache, true);
			hexMesh.RefreshColors(cellDataCache);
		}
	}

	void RefreshDisplayColors ()
	{
		if (mapAsset == null || cellDataList.Count == 0) {
			return;
		}

		if (hexMesh == null) {
			hexMesh = GetComponentInChildren<HexMesh>();
		}

		if (cellDataCache == null || cellDataCache.Length != cellDataList.Count) {
			RebuildCellDataCache();
		}

		for (int i = 0; i < cellDataList.Count; i++) {
			HexCellRenderData data = cellDataList[i];
			data.color = ResolveCellColor(data.coordinates);
			cellDataList[i] = data;
			cellDataCache[i] = data;
		}

		if (hexMesh != null && cellDataCache != null && cellDataCache.Length > 0) {
			hexMesh.RefreshColors(cellDataCache);
		}
	}

	void RebuildCellDataCache ()
	{
		cellDataCache = cellDataList.Count > 0 ? cellDataList.ToArray() : null;
	}

	void AddCellData (HexCoordinates coordinates, Color color)
	{
		cellDataList.Add(HexCellRenderData.Create(coordinates, color));
	}

	Color ResolveCellColor (HexCoordinates coordinates)
	{
		if (!TryFindPointData(coordinates, out HexMapPointData point)) {
			return emptyGridColor;
		}

		Color color;
		if (point.kind == HexPointKind.Path) {
			color = pathColor;
		}
		else if (point.kind == HexPointKind.Event) {
			color = HexEventTypeCatalog.ResolveMapColor(point.eventKind, pathColor);
		}
		else {
			color = pathColor;
		}

		if (point.pointId == selectedPointId) {
			color = Color.Lerp(color, selectedColor, 0.55f);
		}
		return color;
	}

	bool TryFindPointData (HexCoordinates coordinates, out HexMapPointData point)
	{
		for (int i = 0; i < mapAsset.points.Count; i++) {
			HexMapPointData candidate = mapAsset.points[i];
			if (candidate.q == coordinates.X && candidate.r == coordinates.Z) {
				point = candidate;
				return true;
			}
		}
		point = default;
		return false;
	}

	public void SaveToAsset ()
	{
		if (mapAsset == null) {
			return;
		}

		mapAsset.SyncAllPointIds();
		mapAsset.SyncPlayerStartFromStartPoint();

		if (!TryValidateForSave()) {
			return;
		}

#if UNITY_EDITOR
		EditorUtility.SetDirty(mapAsset);
		AssetDatabase.SaveAssets();
		Debug.Log("HexWorldMapEditor: saved " + mapAsset.name);
#else
		Debug.LogWarning("HexWorldMapEditor: save only works in Unity Editor.");
#endif
		isDirty = false;
	}

	public bool RunValidation (bool showDialog)
	{
		if (mapAsset == null) {
			return false;
		}

		mapAsset.SyncAllPointIds();
		mapAsset.SyncPlayerStartFromStartPoint();
		HexWorldMapValidationReport report = HexWorldMapValidator.Validate(mapAsset);
		report.LogToConsole(mapAsset.name);

		if (!showDialog) {
			return !report.HasErrors;
		}

#if UNITY_EDITOR
		if (report.issues.Count == 0) {
			EditorUtility.DisplayDialog(
				"地图校验",
				"未发现配置问题。",
				"确定"
			);
			return true;
		}

		string summary = report.BuildSummary();
		if (report.HasErrors) {
			EditorUtility.DisplayDialog(
				"地图校验未通过",
				"存在 Error，请修复后再保存。\n\n" + summary,
				"确定"
			);
			return false;
		}

		EditorUtility.DisplayDialog(
			"地图校验",
			"存在 Warning，保存前请确认。\n\n" + summary,
			"确定"
		);
#endif
		return !report.HasErrors;
	}

	bool TryValidateForSave ()
	{
		if (mapAsset.points.Count == 0) {
#if UNITY_EDITOR
			EditorUtility.DisplayDialog("无法保存", "地图没有任何点位。", "确定");
#endif
			return false;
		}

		HexWorldMapValidationReport report = HexWorldMapValidator.Validate(mapAsset);
		report.LogToConsole(mapAsset.name);

		if (report.issues.Count == 0) {
			return true;
		}

#if UNITY_EDITOR
		if (report.HasErrors) {
			EditorUtility.DisplayDialog(
				"保存已取消",
				"存在 Error 级别问题，已阻止保存。\n\n" + report.BuildSummary(),
				"确定"
			);
			return false;
		}

		if (report.HasWarnings) {
			return EditorUtility.DisplayDialog(
				"保存确认",
				"存在 Warning，仍要保存吗？\n\n" + report.BuildSummary(),
				"仍要保存",
				"取消"
			);
		}
#endif
		return true;
	}
}
