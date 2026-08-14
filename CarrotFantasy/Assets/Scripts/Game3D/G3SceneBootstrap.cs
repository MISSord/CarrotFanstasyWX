using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 3D 渲染框架的场景构建入口。
    /// 挂在任意空物体上，运行（或编辑器下调用 BuildAll）即可自动搭建：
    /// 透视相机（60° 俯视角）+ 网格地形 + 光照 + 示例 2D 立绘。
    /// 这是"渲染框架演示"，不包含任何战斗逻辑，方便先验证视觉方向。
    /// </summary>
    public class G3SceneBootstrap : MonoBehaviour
    {
        [Header("相机")]
        public Vector3 cameraTarget = new Vector3(0f, 1.5f, 0f);
        public float cameraPitch = 60f;
        public float cameraDistance = 22f;
        public float cameraFov = 45f;
        public bool usePerspective = true;

        [Header("地形")]
        public int terrainRow = 5;
        public int terrainColumn = 8;
        public float cellSize = 2f;

        [Header("示例立绘")]
        [Tooltip("是否摆放演示用 2D 立绘单位。")]
        public bool spawnDemoUnits = true;

        [Tooltip("立绘所在逻辑格（row, column）。")]
        public Vector2Int demoUnitGrid = new Vector2Int(2, 3);

        public G3CameraRig CameraRig { get; private set; }
        public G3GridTerrain Terrain { get; private set; }
        public G3LightingSetup Lighting { get; private set; }

        void Awake()
        {
            BuildAll();
        }

        /// <summary>编辑器 / 运行时统一入口：按当前参数重建整个渲染框架。</summary>
        public void BuildAll()
        {
            BuildLighting();
            BuildTerrain();
            BuildCamera();
            BuildDemoUnits();
        }

        void BuildLighting()
        {
            Lighting = GetComponentInChildren<G3LightingSetup>();
            if (Lighting == null)
            {
                GameObject go = new GameObject("Lighting");
                go.transform.SetParent(transform, false);
                Lighting = go.AddComponent<G3LightingSetup>();
            }
            Lighting.Apply();
        }

        void BuildTerrain()
        {
            Terrain = GetComponentInChildren<G3GridTerrain>();
            if (Terrain == null)
            {
                GameObject go = new GameObject("Terrain");
                go.transform.SetParent(transform, false);
                Terrain = go.AddComponent<G3GridTerrain>();
            }

            Terrain.rowCount = terrainRow;
            Terrain.columnCount = terrainColumn;
            Terrain.cellSize = cellSize;
            Terrain.Build();

            // 地形材质使用 G3/Terrain
            MeshRenderer renderer = Terrain.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("G3/Terrain");
                if (shader != null)
                {
                    renderer.sharedMaterial = new Material(shader);
                }
            }
        }

        void BuildCamera()
        {
            CameraRig = GetComponentInChildren<G3CameraRig>();
            if (CameraRig == null)
            {
                GameObject camGo = new GameObject("G3_MainCamera");
                camGo.transform.SetParent(transform, false);
                Camera camera = camGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                CameraRig = camGo.AddComponent<G3CameraRig>();

                // 设为 MainCamera 标签，供 G3UnitSprite 的 Camera.main 朝向逻辑使用。
                Camera existingMain = Camera.main;
                if (existingMain != null && existingMain != camera)
                {
                    existingMain.tag = "Untagged";
                }
                camera.tag = "MainCamera";
            }

            CameraRig.target = cameraTarget;
            CameraRig.pitch = cameraPitch;
            CameraRig.distance = cameraDistance;
            CameraRig.fieldOfView = cameraFov;
            CameraRig.usePerspective = usePerspective;
            CameraRig.Apply();
        }

        void BuildDemoUnits()
        {
            if (!spawnDemoUnits)
            {
                return;
            }
            if (Terrain == null)
            {
                return;
            }

            // 先清掉旧的示例单位
            Transform oldRoot = transform.Find("DemoUnits");
            if (oldRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(oldRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(oldRoot.gameObject);
                }
            }

            GameObject root = new GameObject("DemoUnits");
            root.transform.SetParent(transform, false);

            // 主角立绘（开深度变换）
            SpawnDemoUnit(root, "DemoUnit_Player", demoUnitGrid.x, demoUnitGrid.y, new Color(1f, 1f, 1f, 1f), 1.15f);
            // 对照组立绘（不开深度变换，用于对比穿模差异）
            SpawnDemoUnit(root, "DemoUnit_NoDepth", demoUnitGrid.x, demoUnitGrid.y + 1, new Color(1f, 0.9f, 0.8f, 1f), 0f);
            // 一只"怪物"立绘
            SpawnDemoUnit(root, "DemoUnit_Enemy", Mathf.Max(0, demoUnitGrid.x - 1), demoUnitGrid.y, new Color(1f, 0.82f, 0.82f, 1f), 1.15f, true);
        }

        void SpawnDemoUnit(
            GameObject parent,
            string name,
            int row,
            int column,
            Color tint,
            float depthStretch,
            bool faceCamera = true)
        {
            GameObject unit = new GameObject(name);
            unit.transform.SetParent(parent.transform, false);
            unit.transform.position = Terrain.WorldPositionOf(row, column);

            G3UnitSprite sprite = unit.AddComponent<G3UnitSprite>();
            sprite.size = new Vector2(1.8f, 2.6f);
            sprite.tintColor = tint;
            sprite.depthStretch = depthStretch;
            sprite.faceCamera = faceCamera;
            sprite.EnsureQuad();
            sprite.EnsureMaterial();
            sprite.Apply();
        }
    }
}
