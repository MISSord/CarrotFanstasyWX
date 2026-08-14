using UnityEngine;

namespace CarrotFantasy.Game3D
{
    /// <summary>
    /// 方形网格 3D 地形生成器，模仿《明日方舟》"网格化高地"场景：
    /// 由 row×column 个凸起平台格组成，合并为单 Mesh（低 DrawCall），
    /// 顶点色驱动每格配色，并提供逻辑坐标 ⇄ 世界坐标映射，供后续玩法（部署/选格/寻路）使用。
    ///
    /// 几何模型（addInnerStep=true 时，一个格 = 两级台阶）：
    ///   - 底部块：y=0 → innerY，水平尺寸 half
    ///   - 顶部块：y=innerY → cellHeight，水平尺寸 innerHalf
    ///   顶面在 cellHeight，形成一个有层次感的凸起平台。
    /// </summary>
    public class G3GridTerrain : MonoBehaviour
    {
        public int rowCount = 5;
        public int columnCount = 8;

        [Tooltip("每个格子的边长（世界单位）。")]
        public float cellSize = 2f;

        [Tooltip("格子平台凸起高度（顶面相对地面）。")]
        public float cellHeight = 0.8f;

        [Tooltip("格子之间的间隙。")]
        public float gap = 0.15f;

        [Tooltip("格子顶面是否加一个更浅的中间层（增加层次感）。")]
        public bool addInnerStep = true;

        public Color deployableColor = new Color(0.55f, 0.6f, 0.5f);
        public Color blockColor = new Color(0.35f, 0.32f, 0.28f);
        public Color sideColor = new Color(0.28f, 0.26f, 0.23f);

        public MeshFilter MeshFilter { get; private set; }
        public MeshCollider MeshCollider { get; private set; }

        public Vector3 GridOrigin { get; private set; }

        const int VerticesPerCellWithStep = 40;
        const int TrianglesPerCellWithStep = 60;
        const int VerticesPerCellFlat = 20;
        const int TrianglesPerCellFlat = 30;

        void Awake()
        {
            Build();
        }

        void OnValidate()
        {
            rowCount = Mathf.Max(1, rowCount);
            columnCount = Mathf.Max(1, columnCount);
        }

        public int CellCount
        {
            get { return rowCount * columnCount; }
        }

        /// <summary>全量重建地形 Mesh。生成后可通过 SetCellColor 修改单格颜色。</summary>
        public void Build()
        {
            // 地面坐标：以整个网格中心为原点，y 轴向上。
            GridOrigin = new Vector3(-(columnCount - 1) * cellSize * 0.5f, 0f, -(rowCount - 1) * cellSize * 0.5f);

            MeshFilter = GetComponent<MeshFilter>();
            if (MeshFilter == null)
            {
                MeshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (GetComponent<MeshRenderer>() == null)
            {
                gameObject.AddComponent<MeshRenderer>();
            }
            MeshCollider = GetComponent<MeshCollider>();
            if (MeshCollider == null)
            {
                MeshCollider = gameObject.AddComponent<MeshCollider>();
            }

            int vertsPerCell = addInnerStep ? VerticesPerCellWithStep : VerticesPerCellFlat;
            int trisPerCell = addInnerStep ? TrianglesPerCellWithStep : TrianglesPerCellFlat;

            Mesh mesh = new Mesh { name = "G3 Grid Terrain" };
            mesh.MarkDynamic();

            Vector3[] vertices = new Vector3[CellCount * vertsPerCell];
            Vector2[] uv = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[CellCount * trisPerCell];

            int vertexCursor = 0;
            int triangleCursor = 0;

            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < columnCount; c++)
                {
                    Vector3 cellOrigin = CellOriginOf(r, c);
                    Color color = (r + c) % 2 == 0 ? deployableColor : deployableColor * 0.9f;

                    if (addInnerStep)
                    {
                        WriteCellWithInnerStep(cellOrigin, color, vertices, uv, colors, triangles, ref vertexCursor, ref triangleCursor);
                    }
                    else
                    {
                        WriteCellFlat(cellOrigin, color, vertices, uv, colors, triangles, ref vertexCursor, ref triangleCursor);
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter.sharedMesh = mesh;
            MeshCollider.sharedMesh = mesh;
        }

        /// <summary>逻辑坐标 (row, column) 的格子中心（地面层 y=0）。</summary>
        Vector3 CellOriginOf(int row, int column)
        {
            return GridOrigin + new Vector3(column * cellSize, 0f, row * cellSize);
        }

        /// <summary>逻辑坐标 (row, column) 的世界坐标（格顶面中心，y=cellHeight）。</summary>
        public Vector3 WorldPositionOf(int row, int column)
        {
            return CellOriginOf(row, column) + new Vector3(0f, cellHeight, 0f);
        }

        /// <summary>世界坐标 → 逻辑坐标；越界返回 false。</summary>
        public bool TryGetGridCoord(Vector3 worldPos, out int row, out int column)
        {
            Vector3 local = worldPos - GridOrigin;
            row = Mathf.RoundToInt(local.z / cellSize);
            column = Mathf.RoundToInt(local.x / cellSize);
            return row >= 0 && row < rowCount && column >= 0 && column < columnCount;
        }

        /// <summary>修改单格顶面颜色。</summary>
        public void SetCellColor(int row, int column, Color color)
        {
            if (MeshFilter == null || MeshFilter.sharedMesh == null || row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            {
                return;
            }

            Mesh mesh = MeshFilter.sharedMesh;
            Color[] meshColors = mesh.colors;
            int vertsPerCell = addInnerStep ? VerticesPerCellWithStep : VerticesPerCellFlat;
            int start = (row * columnCount + column) * vertsPerCell;

            // 顶面 4 个顶点
            for (int i = 0; i < 4; i++)
            {
                meshColors[start + i] = color;
            }
            mesh.colors = meshColors;
        }

        void WriteCellFlat(
            Vector3 origin,
            Color color,
            Vector3[] vertices,
            Vector2[] uv,
            Color[] colors,
            int[] triangles,
            ref int v, ref int t)
        {
            float half = cellSize * 0.5f - gap * 0.5f;

            // 顶面（法线 +Y）
            WriteQuad(origin, half, cellHeight, color, vertices, uv, colors, triangles, ref v, ref t, false);

            // 四侧边（从地面到顶面）
            WriteSideRing(origin, half, 0f, cellHeight, sideColor, vertices, colors, triangles, ref v, ref t);
        }

        void WriteCellWithInnerStep(
            Vector3 origin,
            Color color,
            Vector3[] vertices,
            Vector2[] uv,
            Color[] colors,
            int[] triangles,
            ref int v, ref int t)
        {
            float half = cellSize * 0.5f - gap * 0.5f;
            float innerHalf = half * 0.72f;
            float innerY = cellHeight * 0.45f;

            // 顶面（内层高台顶，法线 +Y）
            WriteQuad(origin, innerHalf, cellHeight, color, vertices, uv, colors, triangles, ref v, ref t, false);

            // 顶部块四侧（innerY → cellHeight）
            WriteSideRing(origin, innerHalf, innerY, cellHeight, color * 0.96f, vertices, colors, triangles, ref v, ref t);

            // 台阶面（底部块顶，法线 +Y）
            WriteQuad(origin, half, innerY, color, vertices, uv, colors, triangles, ref v, ref t, false);

            // 底部块四侧（地面 → innerY）
            WriteSideRing(origin, half, 0f, innerY, sideColor, vertices, colors, triangles, ref v, ref t);
        }

        /// <summary>
        /// 水平面。顶点顺序：BL(-h,-h), BR(h,-h), TR(h,h), TL(-h,h)。
        /// flip=false → 法线 +Y（顶面）；flip=true → 法线 -Y（底面）。
        /// Unity 左手坐标系：正面 = 顶点顺时针。
        /// </summary>
        void WriteQuad(
            Vector3 origin,
            float half,
            float y,
            Color color,
            Vector3[] vertices,
            Vector2[] uv,
            Color[] colors,
            int[] triangles,
            ref int v, ref int t,
            bool flip)
        {
            int a = v;
            vertices[v] = origin + new Vector3(-half, y, -half); uv[v] = new Vector2(0, 0); colors[v] = color; v++;
            vertices[v] = origin + new Vector3(half, y, -half);  uv[v] = new Vector2(1, 0); colors[v] = color; v++;
            vertices[v] = origin + new Vector3(half, y, half);   uv[v] = new Vector2(1, 1); colors[v] = color; v++;
            vertices[v] = origin + new Vector3(-half, y, half);  uv[v] = new Vector2(0, 1); colors[v] = color; v++;

            if (flip)
            {
                // 法线 -Y（底面）：从 +Y 俯视看逆时针（Unity 中逆时针=背面，法线朝 -Y）
                triangles[t++] = a;     triangles[t++] = a + 1; triangles[t++] = a + 2;
                triangles[t++] = a;     triangles[t++] = a + 2; triangles[t++] = a + 3;
            }
            else
            {
                // 法线 +Y（顶面）：从 +Y 俯视看顺时针（Unity 中顺时针=正面，法线朝 +Y）
                triangles[t++] = a;     triangles[t++] = a + 2; triangles[t++] = a + 1;
                triangles[t++] = a;     triangles[t++] = a + 3; triangles[t++] = a + 2;
            }
        }

        /// <summary>四侧边环：从 baseY 到 topY，法线向外。每个格子的侧面用 4 个 quad。</summary>
        /// 每条边传入的 bottom0→bottom1→top0→top1 必须保证"从该面外侧看为顺时针"（Unity 正面）。
        /// 逐面验证：
        ///   边0 (z=-half, 法线 -Z): (-h,-h)→(h,-h)→(h,top)→(-h,top) 从 -Z 看顺时针 ✓
        ///   边1 (z=+half, 法线 +Z): (h,base,h)→(-h,base,h)→(-h,top,h)→(h,top,h) 从 +Z 看顺时针 ✓
        ///   边2 (x=-half, 法线 -X): (-h,base,h)→(-h,base,-h)→(-h,top,-h)→(-h,top,h) 从 -X 看顺时针 ✓
        ///   边3 (x=+half, 法线 +X): (h,base,-h)→(h,base,h)→(h,top,h)→(h,top,-h) 从 +X 看顺时针 ✓
        void WriteSideRing(
            Vector3 origin,
            float half,
            float baseY,
            float topY,
            Color color,
            Vector3[] vertices,
            Color[] colors,
            int[] triangles,
            ref int v, ref int t)
        {
            // 边 0：z = -half，法线 -Z
            WriteSideQuad(origin, half, baseY, topY, color, vertices, colors, triangles, ref v, ref t,
                new Vector3(-half, 0, -half), new Vector3(half, 0, -half), new Vector3(half, 0, -half), new Vector3(-half, 0, -half));

            // 边 1：z = +half，法线 +Z（注意与边 0 的 x 顺序相反，使从 +Z 看为顺时针）
            WriteSideQuad(origin, half, baseY, topY, color, vertices, colors, triangles, ref v, ref t,
                new Vector3(half, 0, half), new Vector3(-half, 0, half), new Vector3(-half, 0, half), new Vector3(half, 0, half));

            // 边 2：x = -half，法线 -X
            WriteSideQuad(origin, half, baseY, topY, color, vertices, colors, triangles, ref v, ref t,
                new Vector3(-half, 0, half), new Vector3(-half, 0, -half), new Vector3(-half, 0, -half), new Vector3(-half, 0, half));

            // 边 3：x = +half，法线 +X（注意与边 2 的 z 顺序相反，使从 +X 看为顺时针）
            WriteSideQuad(origin, half, baseY, topY, color, vertices, colors, triangles, ref v, ref t,
                new Vector3(half, 0, -half), new Vector3(half, 0, half), new Vector3(half, 0, half), new Vector3(half, 0, -half));
        }

        /// <summary>
        /// 单个侧边 quad。corner0/corner1 为底部两端点，corner2/corner3 为顶部两端点（均为 ±half 的 XYZ 分量）。
        /// 顶点顺序 bottom0→bottom1→top0→top1，索引取 (a, a+2, a+1) + (a, a+3, a+2)。
        /// 调用方保证该顺序从该面外侧看为顺时针（Unity 正面，法线朝外）。
        /// </summary>
        void WriteSideQuad(
            Vector3 origin,
            float half,
            float baseY,
            float topY,
            Color color,
            Vector3[] vertices,
            Color[] colors,
            int[] triangles,
            ref int v, ref int t,
            Vector3 bottom0, Vector3 bottom1, Vector3 top0, Vector3 top1)
        {
            int a = v;
            vertices[v] = origin + new Vector3(bottom0.x, baseY, bottom0.z); colors[v] = color; v++;
            vertices[v] = origin + new Vector3(bottom1.x, baseY, bottom1.z); colors[v] = color; v++;
            vertices[v] = origin + new Vector3(top0.x, topY, top0.z);        colors[v] = color; v++;
            vertices[v] = origin + new Vector3(top1.x, topY, top1.z);        colors[v] = color; v++;

            // 顺时针（正面朝外）
            triangles[t++] = a;     triangles[t++] = a + 2; triangles[t++] = a + 1;
            triangles[t++] = a;     triangles[t++] = a + 3; triangles[t++] = a + 2;
        }
    }
}
