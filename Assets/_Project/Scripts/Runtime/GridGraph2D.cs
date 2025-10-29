using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridGraph2D : MonoBehaviour
{
    [Header("网格尺寸")]
    public Vector2 gridWorldSize = new Vector2(20, 12);
    [Tooltip("单元半径（格子大小=半径*2）")]
    public float nodeRadius = 0.25f;

    [Header("可行区域检测")]
    public LayerMask obstacleMask;        // 可留空：用任何2D Collider都视为障碍
    public float obstacleCheckRadius = 0.24f;

    [Header("可视化")]
    public bool drawGrid = true;
    public Color walkableColor = new Color(0.8f, 0.8f, 0.8f, 0.35f);
    public Color unwalkableColor = new Color(0.9f, 0.2f, 0.2f, 0.55f);
    public Color pathColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    Node[,] grid;
    float nodeDiameter;
    int gridSizeX, gridSizeY;

    public int GridSizeX => gridSizeX;
    public int GridSizeY => gridSizeY;
    public float NodeDiameter => nodeDiameter;

    List<Node> debugPath;

    public int MaxSize => gridSizeX * gridSizeY;

    void OnEnable() => CreateGrid();
    void OnValidate() => CreateGrid();

    public void CreateGrid()
    {
        nodeDiameter = nodeRadius * 2f;
        gridSizeX = Mathf.Max(2, Mathf.RoundToInt(gridWorldSize.x / nodeDiameter));
        gridSizeY = Mathf.Max(2, Mathf.RoundToInt(gridWorldSize.y / nodeDiameter));
        grid = new Node[gridSizeX, gridSizeY];

        Vector2 worldBottomLeft = (Vector2)transform.position
                                  - Vector2.right  * gridWorldSize.x / 2f
                                  - Vector2.up     * gridWorldSize.y / 2f;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2 worldPoint = worldBottomLeft
                    + Vector2.right * (x * nodeDiameter + nodeRadius)
                    + Vector2.up    * (y * nodeDiameter + nodeRadius);

                bool walkable = !Physics2D.OverlapCircle(worldPoint, obstacleCheckRadius, obstacleMask.value == 0 ? ~0 : obstacleMask);
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public Node NodeFromWorldPoint(Vector2 worldPos)
    {
        float percentX = Mathf.Clamp01((worldPos.x - (transform.position.x - gridWorldSize.x / 2f)) / gridWorldSize.x);
        float percentY = Mathf.Clamp01((worldPos.y - (transform.position.y - gridWorldSize.y / 2f)) / gridWorldSize.y);
        int x = Mathf.Clamp(Mathf.RoundToInt((gridSizeX - 1) * percentX), 0, gridSizeX - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((gridSizeY - 1) * percentY), 0, gridSizeY - 1);
        return grid[x, y];
    }

    public IEnumerable<Node> GetNeighbours(Node node, bool allowDiagonal)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!allowDiagonal && Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;

                int nx = node.gridX + dx;
                int ny = node.gridY + dy;
                if (nx >= 0 && nx < gridSizeX && ny >= 0 && ny < gridSizeY)
                    yield return grid[nx, ny];
            }
        }
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
            Gizmos.DrawCube(n.worldPos, Vector3.one * (nodeDiameter * 0.95f));
        }

        if (debugPath != null)
        {
            Gizmos.color = pathColor;
            foreach (var n in debugPath)
                Gizmos.DrawCube(n.worldPos, Vector3.one * (nodeDiameter * 0.9f));
        }
    }
}
