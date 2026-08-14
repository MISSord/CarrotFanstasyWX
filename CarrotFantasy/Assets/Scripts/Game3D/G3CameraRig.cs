using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 3D 玩法透视相机 Rig，模仿《明日方舟》固定俯视角战斗相机：
    /// 以目标点为中心，按 pitch（俯仰角）与 distance 环绕，正交/透视可切换。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class G3CameraRig : MonoBehaviour
    {
        [Tooltip("相机注视的目标点（战斗场景中心）。")]
        public Vector3 target = Vector3.zero;

        [Tooltip("俯仰角，60° 为明日方舟典型值（与地面夹角 60°）。")]
        [Range(10f, 85f)]
        public float pitch = 60f;

        [Tooltip("相机与目标点的距离。")]
        public float distance = 18f;

        [Tooltip("视野范围（透视模式）。")]
        public float fieldOfView = 45f;

        [Tooltip("是否使用透视投影；关闭则为正交。")]
        public bool usePerspective = true;

        [Tooltip("正交投影尺寸（usePerspective = false 时生效）。")]
        public float orthoSize = 6f;

        [Tooltip("运行时允许鼠标拖拽改变环绕方位角。")]
        public bool allowRuntimeOrbit = true;

        public float yaw;

        public Camera TargetCamera { get; private set; }

        void Awake()
        {
            TargetCamera = GetComponent<Camera>();
            Apply();
        }

        void OnValidate()
        {
            if (TargetCamera == null)
            {
                TargetCamera = GetComponent<Camera>();
            }
            Apply();
        }

        /// <summary>根据 target/pitch/distance 重新计算相机位置与姿态。</summary>
        public void Apply()
        {
            if (TargetCamera == null)
            {
                TargetCamera = GetComponent<Camera>();
            }
            if (TargetCamera == null)
            {
                return;
            }

            TargetCamera.orthographic = !usePerspective;
            TargetCamera.orthographicSize = orthoSize;
            TargetCamera.fieldOfView = fieldOfView;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 forward = rotation * Vector3.back;
            transform.position = target - forward * distance;
            transform.rotation = rotation;
        }

        void LateUpdate()
        {
            if (allowRuntimeOrbit && Input.GetMouseButton(1) && Input.touchCount == 0)
            {
                yaw += Input.GetAxis("Mouse X") * 3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 1.5f, 10f, 85f);
                Apply();
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance = Mathf.Clamp(distance - scroll * 4f, 4f, 120f);
                Apply();
            }
        }
    }
}
