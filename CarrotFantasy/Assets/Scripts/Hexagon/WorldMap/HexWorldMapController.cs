using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 大地图场景入口：加载资源、处理点击移动、刷新 Mesh。
/// OnGUI 中的快捷键为临时调试手段，正式版应替换为 UI 回调。
/// </summary>
[RequireComponent(typeof(HexMapLayout))]
public class HexWorldMapController : MonoBehaviour
{
	enum PendingInteraction
	{
		None,
		Battle,
		Shop,
		Teleport,
		Random
	}

	public HexWorldMapAsset mapAsset;
	public HexWorldMapView mapView;
	public HexWorldMapFogView fogView;
	public HexMesh hexMesh;

	[Tooltip("为 true 时由 RoguelikeRunManager 处理战斗/商店/随机事件。")]
	public bool useRoguelikeRunOrchestrator = true;

	HexWorldMapRuntime runtime;

	public HexWorldMapAsset MapAsset {
		get { return mapAsset; }
	}

	public HexWorldMapRuntime Runtime {
		get { return runtime; }
	}
	HexMapLayout mapLayout;
	HexMapCameraPan cameraPan;
	PendingInteraction pendingInteraction = PendingInteraction.None;
	int pendingTeleportFromPointId;
	int pendingTeleportTargetPointId;

	void Awake ()
	{
		mapLayout = GetComponent<HexMapLayout>();
		if (mapLayout != null) {
			mapLayout.Apply();
			mapLayout.LayoutChanged += HandleLayoutChanged;
		}
		cameraPan = FindObjectOfType<HexMapCameraPan>();
	}

	void OnDestroy ()
	{
		if (mapLayout != null) {
			mapLayout.LayoutChanged -= HandleLayoutChanged;
		}
	}

	void Start ()
	{
		if (mapAsset == null) {
			Debug.LogError("HexWorldMapController: mapAsset is required.");
			return;
		}

		HexWorldProgress savedProgress = null;
		if (useRoguelikeRunOrchestrator &&
			CarrotFantasy.RoguelikeRunServer.Instance.IsRunActive) {
			savedProgress = CarrotFantasy.RoguelikeRunServer.Instance.GetMapProgress();
		}

		runtime = new HexWorldMapRuntime();
		runtime.Load(mapAsset, savedProgress);
		BindContextEvents(runtime.Context, useRoguelikeRunOrchestrator);

		if (mapView != null) {
			mapView.Build(runtime);
			RebuildMeshGeometry();
		}

		if (fogView != null) {
			fogView.Build(mapAsset, runtime.FogOfWar);
		}

		if (useRoguelikeRunOrchestrator) {
			CarrotFantasy.RoguelikeRunManager.EnsureOn(this);
		}
	}

	void Update ()
	{
		// 镜头平移不受 movementLocked 影响

		if (runtime == null || runtime.MovementLocked) {
			return;
		}
		if (!Input.GetMouseButtonUp(0) ||
			(cameraPan != null && cameraPan.ConsumeMapClick) ||
			EventSystem.current.IsPointerOverGameObject()) {
			return;
		}

		Camera camera = cameraPan != null ? cameraPan.TargetCamera : Camera.main;
		if (camera == null) {
			return;
		}

		Ray ray = camera.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		if (!Physics.Raycast(ray, out hit)) {
			return;
		}

		int pointId;
		if (mapView != null && mapView.TryGetPointIdFromPosition(hit.point, out pointId)) {
			TryMoveToPoint(pointId);
		}
	}

	void HandleLayoutChanged ()
	{
		if (mapView != null && mapView.RefreshLayoutAndColors()) {
			RefreshMeshLayout();
		}
		if (fogView != null) {
			fogView.RefreshLayout();
		}
	}

	/// <summary>
	/// 注册方法回调
	/// </summary>
	/// <param name="context"></param>
	void BindContextEvents (HexMapContext context, bool roguelikeOrchestrator)
	{
		context.OnStateChanged += RefreshVisuals;
		if (!roguelikeOrchestrator) {
			context.OnBattleRequested += HandleBattleRequested;
			context.OnShopRequested += HandleShopRequested;
			context.OnRandomEventRequested += HandleRandomEventRequested;
		}
		context.OnTeleportRequested += HandleTeleportRequested;
		context.OnOneWayTeleportRequested += HandleOneWayTeleportRequested;
		context.OnFragileBroken += HandleFragileBroken;
		context.OnPlayerMoved += HandlePlayerMoved;
	}

	void TryMoveToPoint (int pointId)
	{
		if (runtime.TryMoveTo(pointId)) {
			Debug.Log("Moved to point " + pointId);
		}
	}

	void RefreshVisuals ()
	{
		if (mapView != null && mapView.RefreshColorsOnly()) {
			RefreshMeshColors();
		}
	}

	void RebuildMeshGeometry ()
	{
		if (hexMesh == null || mapView == null) {
			return;
		}

		HexCellRenderData[] cells = mapView.GetCellData();
		if (cells != null && cells.Length > 0) {
			hexMesh.Rebuild(cells);
		}
	}

	void RefreshMeshLayout ()
	{
		if (hexMesh == null || mapView == null) {
			return;
		}

		HexCellRenderData[] cells = mapView.GetCellData();
		if (cells != null && cells.Length > 0) {
			hexMesh.RefreshPositions(cells, true);
			hexMesh.RefreshColors(cells);
		}
	}

	void RefreshMeshColors ()
	{
		if (hexMesh == null || mapView == null) {
			return;
		}

		HexCellRenderData[] cells = mapView.GetCellData();
		if (cells != null && cells.Length > 0) {
			hexMesh.RefreshColors(cells);
		}
	}

	void HandleBattleRequested (int pointId, int encounterId)
	{
		pendingInteraction = PendingInteraction.Battle;
		Debug.Log(
			"Battle requested at point " + pointId + ", encounter " + encounterId +
			". Press Y=win, N=lose."
		);
	}

	void HandleShopRequested (int pointId)
	{
		pendingInteraction = PendingInteraction.Shop;
		Debug.Log("Shop opened at point " + pointId + ". Press C to close.");
	}

	void HandleTeleportRequested (int fromPointId, int targetPointId)
	{
		pendingInteraction = PendingInteraction.Teleport;
		pendingTeleportFromPointId = fromPointId;
		pendingTeleportTargetPointId = targetPointId;
		Debug.Log(
			"Teleport offered from " + fromPointId + " to " + targetPointId +
			". Press T=confirm, N=cancel."
		);
	}

	void HandleOneWayTeleportRequested (int fromPointId, int targetPointId)
	{
		pendingInteraction = PendingInteraction.Teleport;
		pendingTeleportFromPointId = fromPointId;
		pendingTeleportTargetPointId = targetPointId;
		Debug.Log(
			"One-way teleport from " + fromPointId + " to " + targetPointId +
			". Press T=confirm, N=cancel."
		);
	}

	void HandleRandomEventRequested (int pointId, int randomEventId)
	{
		pendingInteraction = PendingInteraction.Random;
		Debug.Log(
			"Random event requested at point " + pointId +
			", event " + randomEventId + ". Press C to close."
		);
	}

	void HandleFragileBroken (int pointId)
	{
		Debug.Log("Fragile point broken: " + pointId);
	}

	void HandlePlayerMoved (int fromPointId, int toPointId)
	{
		Debug.Log("Player moved " + fromPointId + " -> " + toPointId);
		RefreshFogVisuals();
	}

	void RefreshFogVisuals ()
	{
		if (fogView != null) {
			fogView.RefreshColors();
		}
	}

	/// <summary>临时调试 UI，模拟战斗/商店/传送的完成回调。</summary>
	void OnGUI ()
	{
		if (runtime == null || !runtime.MovementLocked) {
			return;
		}

		GUILayout.BeginArea(new Rect(10f, 10f, 340f, 170f), GUI.skin.box);
		GUILayout.Label("Map interaction locked (" + pendingInteraction + ").");
		GUILayout.Label("Y = battle win, N = battle lose / cancel teleport");
		GUILayout.Label("C = close shop / random event, T = confirm teleport");
		GUILayout.EndArea();

		if (Event.current.type != EventType.KeyDown) {
			return;
		}

		HexMapContext context = runtime.Context;
		int currentId = runtime.CurrentPointId;

		if (Event.current.keyCode == KeyCode.Y &&
			pendingInteraction == PendingInteraction.Battle) {
			context.NotifyBattleWon(currentId);
			pendingInteraction = PendingInteraction.None;
		}
		else if (Event.current.keyCode == KeyCode.N &&
			pendingInteraction == PendingInteraction.Battle) {
			context.NotifyBattleLost(currentId);
			pendingInteraction = PendingInteraction.None;
		}
		else if (Event.current.keyCode == KeyCode.C &&
			pendingInteraction == PendingInteraction.Shop) {
			context.NotifyShopClosed();
			pendingInteraction = PendingInteraction.None;
		}
		else if (Event.current.keyCode == KeyCode.C &&
			pendingInteraction == PendingInteraction.Random) {
			context.NotifyRandomEventClosed();
			pendingInteraction = PendingInteraction.None;
		}
		else if (Event.current.keyCode == KeyCode.T &&
			pendingInteraction == PendingInteraction.Teleport) {
			context.ConfirmTeleport(
				pendingTeleportFromPointId,
				pendingTeleportTargetPointId,
				true
			);
			pendingInteraction = PendingInteraction.None;
		}
		else if (Event.current.keyCode == KeyCode.N &&
			pendingInteraction == PendingInteraction.Teleport) {
			context.ConfirmTeleport(
				pendingTeleportFromPointId,
				pendingTeleportTargetPointId,
				false
			);
			pendingInteraction = PendingInteraction.None;
		}
	}
}
