using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 六边形地图镜头控制：WASD / 左键拖动平移，滚轮缩放（可设上下限）。
/// 拖动与点击区分：拖动超过阈值后释放左键时不触发地图点击。
/// </summary>
[DefaultExecutionOrder(-100)]
public class HexMapCameraPan : MonoBehaviour
{
	public enum PerspectiveZoomMode
	{
		/// <summary>沿视线与地面的交点前后移动，min/max 为距离。</summary>
		DistanceToGround,
		/// <summary>直接修改 Field of View，min/max 为 FOV 角度。</summary>
		FieldOfView
	}

	[SerializeField]
	Camera targetCamera;

	[Header("Pan")]
	[SerializeField]
	[Tooltip("WASD 移动速度（单位/秒）。")]
	float keyboardPanSpeed = 30f;

	[SerializeField]
	[Tooltip("鼠标拖动平移灵敏度（像素 → 世界单位）。")]
	float dragPanScale = 0.08f;

	[SerializeField]
	[Tooltip("超过该像素距离视为拖动，释放左键时不触发地图点击。")]
	float dragThreshold = 6f;

	[SerializeField]
	bool blockWhenPointerOverUI = true;

	[Header("Zoom")]
	[SerializeField]
	[Tooltip("滚轮缩放灵敏度。")]
	float scrollZoomSpeed = 5f;

	[SerializeField]
	[Tooltip("Orthographic：orthographicSize 下限（越近）。Perspective Distance：与地面交点最近距离。")]
	float minZoom = 8f;

	[SerializeField]
	[Tooltip("Orthographic：orthographicSize 上限（越远）。Perspective Distance：与地面交点最远距离。")]
	float maxZoom = 60f;

	[SerializeField]
	PerspectiveZoomMode perspectiveZoomMode = PerspectiveZoomMode.DistanceToGround;

	[SerializeField]
	[Tooltip("透视 Distance 模式时，射线未命中地面则沿 forward 平移。")]
	float groundPlaneHeight = 0f;

	Func<bool> additionalInputBlock;

	Vector3 lastMousePosition;
	bool trackingMouse;
	bool dragTriggered;
	float currentOrthographicSize;
	float currentFieldOfView;
	bool orthographicZoomInitialized;
	bool fieldOfViewZoomInitialized;

	/// <summary>本帧左键释放时是否因拖动而应屏蔽地图点击。</summary>
	public bool ConsumeMapClick { get; private set; }

	/// <summary>当前是否处于左键拖动平移中。</summary>
	public bool IsDragging {
		get { return dragTriggered && Input.GetMouseButton(0); }
	}

	public Camera TargetCamera {
		get { return targetCamera; }
	}

	void Awake ()
	{
		if (targetCamera == null) {
			targetCamera = GetComponent<Camera>();
		}
		if (targetCamera == null) {
			targetCamera = Camera.main;
		}
		InitializeZoomState();
	}

	void Start ()
	{
		ClampZoomToLimitsAtStart();
	}

	void InitializeZoomState ()
	{
		if (targetCamera == null) {
			return;
		}

		if (targetCamera.orthographic) {
			currentOrthographicSize = targetCamera.orthographicSize;
			orthographicZoomInitialized = true;
			return;
		}

		if (perspectiveZoomMode == PerspectiveZoomMode.FieldOfView) {
			currentFieldOfView = targetCamera.fieldOfView;
			fieldOfViewZoomInitialized = true;
		}
	}

	/// <summary>游戏开始时若缩放超出范围，立即拉到 min/max 边界。</summary>
	void ClampZoomToLimitsAtStart ()
	{
		if (targetCamera == null) {
			return;
		}

		if (targetCamera.orthographic) {
			currentOrthographicSize = Mathf.Clamp(
				targetCamera.orthographicSize,
				minZoom,
				maxZoom
			);
			targetCamera.orthographicSize = currentOrthographicSize;
			orthographicZoomInitialized = true;
			return;
		}

		if (perspectiveZoomMode == PerspectiveZoomMode.FieldOfView) {
			currentFieldOfView = Mathf.Clamp(
				targetCamera.fieldOfView,
				minZoom,
				maxZoom
			);
			targetCamera.fieldOfView = currentFieldOfView;
			fieldOfViewZoomInitialized = true;
			return;
		}

		ClampPerspectiveGroundDistance();
	}

	void ClampPerspectiveGroundDistance ()
	{
		if (!TryGetGroundFocus(out Vector3 focus, out Vector3 forward, out float distance)) {
			return;
		}

		if (distance < minZoom) {
			targetCamera.transform.position = focus - forward * minZoom;
		}
		else if (distance > maxZoom) {
			targetCamera.transform.position = focus - forward * maxZoom;
		}
	}

	void OnValidate ()
	{
		if (maxZoom < minZoom) {
			maxZoom = minZoom;
		}
	}

	void Update ()
	{
		ConsumeMapClick = false;

		if (targetCamera == null) {
			return;
		}

		if (!IsInputBlocked()) {
			HandleScrollZoom();
			HandleKeyboardPan();
			HandleMouseDragPan();
		}
		else if (Input.GetMouseButtonUp(0)) {
			ResetMouseTracking();
		}

		if (Input.GetMouseButtonUp(0)) {
			if (dragTriggered) {
				ConsumeMapClick = true;
			}
			ResetMouseTracking();
		}
	}

	public void SetInputBlocker (Func<bool> blocker)
	{
		additionalInputBlock = blocker;
	}

	public void ClearInputBlocker ()
	{
		additionalInputBlock = null;
	}

	bool IsInputBlocked ()
	{
		if (blockWhenPointerOverUI &&
			EventSystem.current != null &&
			EventSystem.current.IsPointerOverGameObject()) {
			return true;
		}
		if (additionalInputBlock != null && additionalInputBlock()) {
			return true;
		}
		return false;
	}

	void HandleScrollZoom ()
	{
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		if (Mathf.Approximately(scroll, 0f)) {
			return;
		}

		if (targetCamera.orthographic) {
			if (!orthographicZoomInitialized) {
				currentOrthographicSize = targetCamera.orthographicSize;
				orthographicZoomInitialized = true;
			}
			currentOrthographicSize = ApplySoftZoomLimit(
				currentOrthographicSize,
				-scroll * scrollZoomSpeed,
				minZoom,
				maxZoom
			);
			targetCamera.orthographicSize = currentOrthographicSize;
			return;
		}

		if (perspectiveZoomMode == PerspectiveZoomMode.FieldOfView) {
			if (!fieldOfViewZoomInitialized) {
				currentFieldOfView = targetCamera.fieldOfView;
				fieldOfViewZoomInitialized = true;
			}
			currentFieldOfView = ApplySoftZoomLimit(
				currentFieldOfView,
				-scroll * scrollZoomSpeed * 4f,
				minZoom,
				maxZoom
			);
			targetCamera.fieldOfView = currentFieldOfView;
			return;
		}

		ZoomPerspectiveByGroundDistance(scroll);
	}

	/// <summary>
	/// 从当前值增量缩放；若初始值在范围外，先平滑进入范围，避免首次滚轮跳跃。
	/// </summary>
	static float ApplySoftZoomLimit (
		float current,
		float delta,
		float min,
		float max
	) {
		float next = current + delta;

		if (current > max) {
			if (delta > 0f) {
				return current;
			}
			return next < max ? max : next;
		}

		if (current < min) {
			if (delta < 0f) {
				return current;
			}
			return next > min ? min : next;
		}

		return Mathf.Clamp(next, min, max);
	}

	void ZoomPerspectiveByGroundDistance (float scroll)
	{
		Transform camTransform = targetCamera.transform;
		Vector3 forward = camTransform.forward;
		camTransform.position += forward * (scroll * scrollZoomSpeed);

		if (!TryGetGroundFocus(out Vector3 focus, out forward, out float distance)) {
			return;
		}

		// 仅在试图突破边界时修正，已在范围外向内缩放时不瞬间跳转
		if (distance < minZoom && scroll > 0f) {
			camTransform.position = focus - forward * minZoom;
		}
		else if (distance > maxZoom && scroll < 0f) {
			camTransform.position = focus - forward * maxZoom;
		}
	}

	bool TryGetGroundFocus (out Vector3 focus, out Vector3 forward, out float distance)
	{
		focus = default;
		forward = targetCamera.transform.forward;
		Plane ground = new Plane(Vector3.up, new Vector3(0f, groundPlaneHeight, 0f));
		Ray centerRay = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

		if (!ground.Raycast(centerRay, out float enter)) {
			distance = 0f;
			return false;
		}

		focus = centerRay.GetPoint(enter);
		distance = enter;
		return true;
	}

	void HandleKeyboardPan ()
	{
		float horizontal = 0f;
		float vertical = 0f;

		if (Input.GetKey(KeyCode.A)) {
			horizontal -= 1f;
		}
		if (Input.GetKey(KeyCode.D)) {
			horizontal += 1f;
		}
		if (Input.GetKey(KeyCode.S)) {
			vertical -= 1f;
		}
		if (Input.GetKey(KeyCode.W)) {
			vertical += 1f;
		}

		if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f)) {
			return;
		}

		Vector3 move = GetPlanarCameraDirection(horizontal, vertical);
		move *= keyboardPanSpeed * Time.deltaTime;
		ApplyPan(move);
	}

	void HandleMouseDragPan ()
	{
		if (Input.GetMouseButtonDown(0)) {
			lastMousePosition = Input.mousePosition;
			trackingMouse = true;
			dragTriggered = false;
			return;
		}

		if (!trackingMouse || !Input.GetMouseButton(0)) {
			return;
		}

		Vector3 current = Input.mousePosition;
		Vector3 delta = current - lastMousePosition;
		if (!dragTriggered && delta.magnitude >= dragThreshold) {
			dragTriggered = true;
		}

		if (dragTriggered) {
			Vector3 move = GetPlanarCameraDirection(-delta.x, -delta.y);
			move *= dragPanScale;
			ApplyPan(move);
			lastMousePosition = current;
		}
	}

	void ResetMouseTracking ()
	{
		trackingMouse = false;
		dragTriggered = false;
	}

	Vector3 GetPlanarCameraDirection (float horizontal, float vertical)
	{
		Vector3 right = targetCamera.transform.right;
		Vector3 forward = targetCamera.transform.forward;
		right.y = 0f;
		forward.y = 0f;

		if (right.sqrMagnitude < 0.0001f) {
			right = Vector3.right;
		}
		else {
			right.Normalize();
		}

		if (forward.sqrMagnitude < 0.0001f) {
			forward = Vector3.forward;
		}
		else {
			forward.Normalize();
		}

		return right * horizontal + forward * vertical;
	}

	void ApplyPan (Vector3 worldDelta)
	{
		targetCamera.transform.position += worldDelta;
	}
}
