using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 随机障碍生成器（2D 网格）
/// - 按密度在网格上随机生成墙体（支持保护角色周围空地）
/// - 在 ObstaclesRoot 下实例化墙块（SpriteRenderer+BoxCollider2D 可选）
/// - 同步写入 GridGraph2D 的阻塞表，A* 即可使用
/// 触发：
///   - 运行时按 G
///   - Inspector 上下文菜单 [ContextMenu("生成随机障碍")]
///   - UI Button 绑定 Generate()
/// </summary>
[DisallowMultipleComponent]
public class MazeRandomGenerator2D : MonoBehaviour
{
    [Header("引用")]
    public GridGraph2D grid;                 // 指向当前使用的 GridGraph2D
    public Transform obstaclesRoot;          // 所有墙体的父节点
    public Transform agent;                  // 需要避让的角色

    [Header("尺寸（格子数）")]
    [Tooltip("逻辑尺寸(列 x 行)，与 GridGraph2D 对齐")]
    public int columns = 20;                 // 列数
    public int rows = 12;                    // 行数

    [Header("随机障碍参数")]
    [Range(0f, 0.9f)] public float obstacleDensity = 0.25f; // 障碍密度（0~0.9）
    public int randomSeed = 0;                                // 0=自动随机
    public bool protectAgentArea = true;                      // 保护角色附近空地
    [Range(0, 3)] public int protectRadius = 1;               // 保护半径（格）

    [Header("外观与碰撞")]
    public bool addSpriteRenderer = true;    // 是否给墙体添加 SpriteRenderer
    public Sprite wallSprite;                // 可选：墙体精灵
    public Vector2 wallTilePadding = new Vector2(0.95f, 0.95f); // 视觉缩放比例
    public string wallLayerName = "";        // 可选：墙体图层名
    public PhysicsMaterial2D wallPhysicsMaterial; // 可选：墙体物理材质

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
        if (autoRegenerateOnPlay) Generate();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G)) Generate();
    }

    [ContextMenu("生成随机障碍")]
    public void Generate()
    {
        if (!ValidateSetup()) return;

        // 1) 计算与 GridGraph2D 对齐的网格几何
        SyncWithGrid();

        // 2) 生成布尔阵列（true=通路/可走；false=墙）
        bool[,] passable = GenerateRandomPassableArray(columns, rows, obstacleDensity, randomSeed);

        // 3) 清空旧的墙体
        ClearObstacles();

        // 4) 按 passable 实例化墙块
        BuildWalls(passable);

        // 5) 将墙体写入 GridGraph2D 的阻塞标记
        grid.ClearAllBlocks();
        ApplyBlocksToGrid(passable);

        Debug.Log($"[MazeRandomGenerator2D] 已生成随机障碍：尺寸={columns}x{rows}，节点尺寸={nodeDiameter:F2}");
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


    // 随机障碍：根据密度生成阻塞，保护角色周围一定半径
    bool[,] GenerateRandomPassableArray(int cols, int rws, float density, int seed)
    {
        bool[,] pass = new bool[cols, rws];
        var rng = (seed == 0) ? new System.Random() : new System.Random(seed);
        density = Mathf.Clamp01(density);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rws; y++)
            {
                // true=可走，按密度反向取样
                pass[x, y] = rng.NextDouble() >= density;
            }
        }

        // 保护角色周围空地
        if (protectAgentArea && agent != null && grid != null)
        {
            var n = grid.NodeFromWorldPoint(agent.position);
            int cx = Mathf.Clamp(n.gridX, 0, cols - 1);
            int cy = Mathf.Clamp(n.gridY, 0, rws - 1);
            int r = Mathf.Max(0, protectRadius);
            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x >= 0 && x < cols && y >= 0 && y < rws)
                        pass[x, y] = true;
                }
            }
        }

        return pass;
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

                // 碰撞体（可选，仅用于物理阻挡，与网格阻塞无关）
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(nodeDiameter * wallTilePadding.x, nodeDiameter * wallTilePadding.y);
                bc.sharedMaterial = wallPhysicsMaterial;
                bc.usedByComposite = false;
            }
        }
    }

    // 将 passable 阵列写入 GridGraph2D
    void ApplyBlocksToGrid(bool[,] passable)
    {
        int cols = passable.GetLength(0);
        int rws  = passable.GetLength(1);
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rws; y++)
            {
                bool blocked = !passable[x, y];
                grid.SetBlock(x, y, blocked);
            }
        }
    }
}
