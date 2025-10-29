using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 随机迷宫生成（2D 网格）
/// - 使用“递归回溯 / 深度优先”挖迷宫，奇数格为通路、偶数格为墙
/// - 在 ObstaclesRoot 下实例化墙块（带 BoxCollider2D 可选 SpriteRenderer）
/// - 生成后自动调用 GridGraph2D.CreateGrid() 让 A* 使用最新障碍
/// 触发：
///   - 运行时按 G
///   - Inspector 上下文菜单 [ContextMenu("立即生成迷宫")]
///   - UI Button 绑定 GenerateMaze()
/// </summary>
[DisallowMultipleComponent]
public class MazeRandomGenerator2D : MonoBehaviour
{
    [Header("引用")]
    public GridGraph2D grid;                 // 指向当前使用的 GridGraph2D
    public Transform obstaclesRoot;          // 所有墙体的父节点
    public Transform agent;                  // 需要避让的角色

    [Header("迷宫尺寸（格子数）")]
    [Tooltip("迷宫逻辑尺寸(列 x 行)，需与 GridGraph2D 的网格采样一致")]
    public int columns = 20;                 // 列数
    public int rows = 12;                    // 行数

    [Header("外观与碰撞")]
    public bool addSpriteRenderer = true;    // 是否给墙体添加 SpriteRenderer
    public Sprite wallSprite;                // 可选：墙体精灵
    public Vector2 wallTilePadding = new Vector2(0.95f, 0.95f); // 视觉缩放比例
    public string wallLayerName = "";        // 可选：墙体图层名
    public PhysicsMaterial2D wallPhysicsMaterial; // 可选：墙体物理材质

    [Header("随机种子")]
    public int seed = 0;                     // 0 表示自动随机

    [Header("调试")]
    public bool autoRegenerateOnPlay = false; // Play 时是否自动生成

    float nodeDiameter;   // 与 GridGraph2D 对齐
    Vector2 worldBottomLeft;

    void Reset()
    {
        grid = GetComponent<GridGraph2D>();
    }

    void Start()
    {
        if (autoRegenerateOnPlay) GenerateMaze();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            GenerateMaze();
        }
    }

    [ContextMenu("立即生成迷宫")]
    public void GenerateMaze()
    {
        if (!ValidateSetup()) return;

        // 1) 计算与 GridGraph2D 对齐的网格几何
        SyncWithGrid();

        // 2) 生成布尔阵列（true=通路/可走；false=墙）
        var passable = GeneratePassableArray(columns, rows);

        // 3) 清空旧的墙体
        ClearObstacles();

        // 4) 按 passable 实例化墙块
        BuildWalls(passable);

        // 5) 让 GridGraph2D 重建可走/不可走
        grid.CreateGrid();

        Debug.Log($"[MazeRandomGenerator2D] 已生成迷宫：{columns}x{rows}，节点尺寸={nodeDiameter:F2}");
    }

    bool ValidateSetup()
    {
        if (grid == null)
        {
            Debug.LogError("[MazeRandomGenerator2D] 未指定 GridGraph2D。");
            return false;
        }
        if (obstaclesRoot == null)
        {
            Debug.LogError("[MazeRandomGenerator2D] 未指定墙体父物体。");
            return false;
        }
        if (columns < 5) columns = 5;
        if (rows < 5) rows = 5;
        return true;
    }

    void SyncWithGrid()
    {
        nodeDiameter = Mathf.Max(0.0001f, grid.nodeRadius * 2f);

        worldBottomLeft = (Vector2)grid.transform.position
                        - Vector2.right * grid.gridWorldSize.x / 2f
                        - Vector2.up    * grid.gridWorldSize.y / 2f;

        columns = Mathf.Max(5, grid.GridSizeX);
        rows = Mathf.Max(5, grid.GridSizeY);
    }

    bool[,] GeneratePassableArray(int cols, int rws)
    {
        // 递归回溯：奇数坐标为“房间”，偶数为墙，起点(1,1)
        bool[,] pass = new bool[cols, rws];

        // 全部置墙
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rws; y++)
                pass[x, y] = false;

        System.Random rng = (seed == 0) ? new System.Random() : new System.Random(seed);

        // 起点
        int sx = 1, sy = 1;
        pass[sx, sy] = true;

        // 深度优先栈
        Stack<(int x, int y)> st = new Stack<(int x, int y)>();
        st.Push((sx, sy));

        (int x, int y)[] dirs = new (int, int)[] { (2,0), (-2,0), (0,2), (0,-2) };

        while (st.Count > 0)
        {
            var cur = st.Pop();

            // 打乱邻居方向
            Shuffle(dirs, rng);

            foreach (var d in dirs)
            {
                int nx = cur.x + d.x;
                int ny = cur.y + d.y;

                if (nx > 0 && nx < cols - 1 && ny > 0 && ny < rws - 1 && !pass[nx, ny])
                {
                    // 挖通中间墙
                    int wx = cur.x + d.x / 2;
                    int wy = cur.y + d.y / 2;
                    pass[wx, wy] = true;
                    pass[nx, ny] = true;

                    st.Push((cur.x, cur.y)); // 回溯点
                    st.Push((nx, ny));
                    break;
                }
            }
        }

        // 入口/出口（可选）
        pass[1, 0] = true;                // 顶部开口
        pass[cols - 2, rws - 1] = true;   // 底部开口

        return pass;
    }

    void Shuffle((int x, int y)[] a, System.Random rng)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            var t = a[i]; a[i] = a[k]; a[k] = t;
        }
    }

    void ClearObstacles()
    {
        // 清空 ObstaclesRoot 旧子物体
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform c in obstaclesRoot) toDestroy.Add(c.gameObject);

        for (int i = 0; i < toDestroy.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) GameObject.DestroyImmediate(toDestroy[i]);
            else GameObject.Destroy(toDestroy[i]);
#else
            GameObject.Destroy(toDestroy[i]);
#endif
        }
    }

    void BuildWalls(bool[,] passable)
    {
        int cols = passable.GetLength(0);
        int rws  = passable.GetLength(1);

        // 可选：设置 Layer
        int wallLayer = -1;
        if (!string.IsNullOrEmpty(wallLayerName))
        {
            wallLayer = LayerMask.NameToLayer(wallLayerName);
            if (wallLayer == -1) Debug.LogWarning($"[MazeRandomGenerator2D] 图层 '{wallLayerName}' 不存在，将使用默认图层。");
        }

        // 逐格生成墙体（可走=false）
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rws; y++)
            {
                if (passable[x, y]) continue;

                Vector2 cellCenter = worldBottomLeft
                    + Vector2.right * (x * nodeDiameter + (nodeDiameter * 0.5f))
                    + Vector2.up    * (y * nodeDiameter + (nodeDiameter * 0.5f));

                // 检查该位置是否与角色重叠
                if (agent != null)
                {
                    float distToAgent = Vector2.Distance(cellCenter, agent.position);
                    // 预留 3×3 区域，保证角色有空间移动
                    if (distToAgent < nodeDiameter * 1.5f)
                    {
                        // 与角色太近则跳过
                        continue;
                    }
                }

                var go = new GameObject($"Wall_{x}_{y}");
                go.transform.SetParent(obstaclesRoot, worldPositionStays: false);
                go.transform.position = cellCenter;
                if (wallLayer != -1) go.layer = wallLayer;

                // 可视化
                if (addSpriteRenderer)
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = wallSprite;
                    // 让尺寸贴合格子大小
                    float sx = nodeDiameter * wallTilePadding.x;
                    float sy = nodeDiameter * wallTilePadding.y;
                    go.transform.localScale = new Vector3(
                        sr.sprite ? sx / (sr.sprite.bounds.size.x) : sx,
                        sr.sprite ? sy / (sr.sprite.bounds.size.y) : sy,
                        1f);
                }
                else
                {
                    // 如果没有精灵，则保持缺省缩放
                }

                // 碰撞体（供 GridGraph2D 检测）
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(nodeDiameter * wallTilePadding.x, nodeDiameter * wallTilePadding.y);
                bc.sharedMaterial = wallPhysicsMaterial;
                bc.usedByComposite = false;
            }
        }
    }
}
