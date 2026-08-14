using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 2D 立绘单位组件：在 3D 场景中渲染一个面向相机的立绘面片，
    /// 使用 G3/UnitSprite（Unlit + 可选深度变换）材质。
    /// 深度变换的 pivot 使用面片自身 transform 位置（物体原点即脚底）。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class G3UnitSprite : MonoBehaviour
    {
        [Tooltip("立绘贴图。为空时自动生成占位纹理。")]
        public Texture2D spriteTexture;

        [Tooltip("立绘面片尺寸（世界单位）。")]
        public Vector2 size = new Vector2(1.6f, 2.2f);

        [Tooltip("是否面向相机。关闭时保持场景摆放方向。")]
        public bool faceCamera = true;

        [Tooltip("深度变换强度。0=关闭；60° 相机理论值约 1.15~2.0。")]
        [Range(0f, 4f)]
        public float depthStretch = 0f;

        [Tooltip("着色基调，用于场景氛围融合（明日方舟每场景一套 tint color）。")]
        public Color tintColor = Color.white;

        public Renderer TargetRenderer { get; private set; }
        public Material TargetMaterial { get; private set; }

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int DepthStretchId = Shader.PropertyToID("_DepthStretch");
        static readonly int FaceCameraId = Shader.PropertyToID("_FaceCamera");

        void Awake()
        {
            EnsureQuad();
            EnsureMaterial();
            Apply();
        }

        void OnValidate()
        {
            if (TargetRenderer == null)
            {
                TargetRenderer = GetComponent<MeshRenderer>();
            }
            if (TargetRenderer != null && TargetMaterial == null)
            {
                TargetMaterial = TargetRenderer.sharedMaterial;
            }
            Apply();
        }

        /// <summary>创建朝向 +Z 的 Quad 面片网格（尺寸可调）。</summary>
        public void EnsureQuad()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            Mesh mesh = filter.sharedMesh;
            if (mesh != null && mesh.name == "G3 Unit Quad")
            {
                return;
            }

            mesh = new Mesh { name = "G3 Unit Quad" };
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0f, 0f),
                new Vector3(hx, 0f, 0f),
                new Vector3(hx, hy, 0f),
                new Vector3(-hx, hy, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            filter.sharedMesh = mesh;
            if (GetComponent<MeshCollider>() == null)
            {
                MeshCollider collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        /// <summary>创建（或复用）G3/UnitSprite 材质实例。</summary>
        public void EnsureMaterial()
        {
            TargetRenderer = GetComponent<MeshRenderer>();
            if (TargetRenderer == null)
            {
                return;
            }

            if (TargetMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("G3/UnitSprite");
            if (shader == null)
            {
                Debug.LogError("[G3UnitSprite] 找不到 G3/UnitSprite shader，请确认 Shader 已导入。");
                return;
            }

            TargetMaterial = new Material(shader);
            TargetMaterial.name = "G3 Unit Sprite Material";
            TargetRenderer.sharedMaterial = TargetMaterial;
        }

        /// <summary>把参数同步到材质与 transform（朝向相机）。</summary>
        public void Apply()
        {
            if (spriteTexture == null)
            {
                spriteTexture = G3PlaceholderTexture.Default;
            }
            if (TargetMaterial == null)
            {
                EnsureMaterial();
            }
            if (TargetMaterial == null)
            {
                return;
            }

            TargetMaterial.SetTexture(MainTexId, spriteTexture);
            TargetMaterial.SetColor(TintId, tintColor);
            TargetMaterial.SetFloat(DepthStretchId, depthStretch);
            TargetMaterial.SetFloat(FaceCameraId, faceCamera ? 1f : 0f);
        }

        void LateUpdate()
        {
            if (faceCamera && Camera.main != null)
            {
                // 绕自身 Y 轴面向相机，保持"站立"姿态；深度变换由 shader 内部完成。
                Vector3 lookDir = Camera.main.transform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }
        }
    }
}
