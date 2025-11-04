using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridGraph2D : MonoBehaviour
{
    [Header("网格尺寸（仅需两个参数）")]
    public int columns = 20;                 // 横向格子数
    public int rows = 12;                    // 纵向格子数

    [Header("可视化")]
    public bool drawGrid = true;
    public Color walkableColor = new Color(0.8f, 0.8f, 0.8f, 0.35f);
    public Color unwalkableColor = new Color(0.9f, 0.2f, 0.2f, 0.55f);
    public Color pathColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    // 内部网格
    Node[,] grid;

    // 统一单元尺寸：1 个世界单位
    const float cellSize = 1f;

    // 兼容旧接口（供其它脚本读取）
    public int GridSizeX => columns;
    public int GridSizeY => rows;
    public float NodeDiameter => cellSize;
    public float nodeRadius => cellSize * 0.5f;
    public Vector2 gridWorldSize => new Vector2(columns * cellSize, rows * cellSize);

    List<Node> debugPath;

    public int MaxSize => columns * rows;

    void OnEnable() => CreateGrid();
    void OnValidate() => CreateGrid();

    public void CreateGrid()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        grid = new Node[columns, rows];

        Vector2 worldBottomLeft = (Vector2)transform.position
                                  - Vector2.right  * (columns * cellSize) / 2f
                                  - Vector2.up     * (rows * cellSize) / 2f;

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector2 worldPoint = worldBottomLeft
                    + Vector2.right * (x * cellSize + nodeRadius)
                    + Vector2.up    * (y * cellSize + nodeRadius);

                // 不再做可行区域检测：默认全部可走
                grid[x, y] = new Node(true, worldPoint, x, y);
            }
        }
    }

    public Node NodeFromWorldPoint(Vector2 worldPos)
    {
        Vector2 worldBottomLeft = (Vector2)transform.position
                                  - Vector2.right  * (columns * cellSize) / 2f
                                  - Vector2.up     * (rows * cellSize) / 2f;

        float dx = worldPos.x - worldBottomLeft.x;
        float dy = worldPos.y - worldBottomLeft.y;
        int x = Mathf.Clamp(Mathf.FloorToInt(dx / cellSize), 0, columns - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(dy / cellSize), 0, rows - 1);
        return grid[x, y];
    }

    public IEnumerable<Node> GetNeighbours(Node node)
    {
        // 仅四方向邻居（上、下、左、右）
        int x = node.gridX;
        int y = node.gridY;

        if (x - 1 >= 0) yield return grid[x - 1, y];
        if (x + 1 < columns) yield return grid[x + 1, y];
        if (y - 1 >= 0) yield return grid[x, y - 1];
        if (y + 1 < rows) yield return grid[x, y + 1];
    }

    // 供外部手动标记阻塞（替代物理检测）
    public void SetBlock(int x, int y, bool blocked)
    {
        if (x < 0 || x >= columns || y < 0 || y >= rows) return;
        grid[x, y].walkable = !blocked;
    }

    public void BlockNode(Node n)
    {
        if (n == null) return;
        SetBlock(n.gridX, n.gridY, true);
    }

    public void ClearAllBlocks()
    {
        if (grid == null) return;
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                grid[x, y].walkable = true;
    }

    public void SetDebugPath(List<Node> path) => debugPath = path;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 0));

        if (!drawGrid || grid == null) return;

        foreach (var n in grid)
        {
            Gizmos.color = n.walkable ? walkableColor : unwalkableColor;
            Gizmos.DrawCube(n.worldPos, Vector3.one * (NodeDiameter * 0.95f));
        }

        if (debugPath != null)
        {
            Gizmos.color = pathColor;
            foreach (var n in debugPath)
                Gizmos.DrawCube(n.worldPos, Vector3.one * (NodeDiameter * 0.9f));
        }
    }
}
