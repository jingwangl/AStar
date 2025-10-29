using System.Collections.Generic;
using UnityEngine;

public class PathfindingAStar : MonoBehaviour
{
    [Header("Refs")]
    public GridGraph2D grid;            // 拖拽 GridGraph2D
    public Transform agent;             // 你的小球
    public LineRenderer line;           // 用于显示路径（可空）
    public bool allowDiagonal = false;

    [Header("Move")]
    public float moveSpeed = 4f;
    public float waypointTolerance = 0.05f;

    List<Node> currentPath;
    bool moving;

    void Reset()
    {
        grid = GetComponent<GridGraph2D>();
    }

    void Update()
    {
        // 右键选择终点并自动寻路移动
        if (Input.GetMouseButtonDown(1))
        {
            if (agent == null)
            {
                Debug.LogWarning("Agent 未指定，请在 Inspector 中拖拽。");
            }
            else
            {
                Vector2 targetWorldPos = ScreenToWorld(Input.mousePosition);
                HandleTargetSelection(targetWorldPos);
            }
        }

#if UNITY_EDITOR
        // R：重建网格（调试用）
        if (Input.GetKeyDown(KeyCode.R))
        {
            grid.CreateGrid();
            Debug.Log("已重新生成网格");
        }
#endif
    }

    Vector2 ScreenToWorld(Vector3 mouse)
    {
        var w = Camera.main.ScreenToWorldPoint(mouse);
        return new Vector2(w.x, w.y);
    }

    void HandleTargetSelection(Vector2 targetWorldPos)
    {
        if (grid == null)
        {
            Debug.LogWarning("Grid 未指定。");
            return;
        }

        var startNode = grid.NodeFromWorldPoint(agent.position);
        var targetNode = grid.NodeFromWorldPoint(targetWorldPos);

        var oldPath = currentPath;
        var newPath = FindPath(startNode, targetNode, allowDiagonal);

        if (newPath != null && newPath.Count > 0)
        {
            currentPath = newPath;
            grid.SetDebugPath(newPath);
            ApplyLineRenderer(newPath);

            moving = false;
            StopAllCoroutines();
            StartCoroutine(FollowPath());
        }
        else
        {
            currentPath = oldPath;
            grid.SetDebugPath(oldPath);
            ApplyLineRenderer(oldPath);
            Debug.LogWarning("目标不可达，已保持原路径。");
        }
    }

    void ApplyLineRenderer(List<Node> path)
    {
        if (line == null) return;

        if (path == null || path.Count == 0)
        {
            line.positionCount = 0;
            return;
        }

        line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            line.SetPosition(i, path[i].worldPos);
    }

    System.Collections.IEnumerator FollowPath()
    {
        if (moving || agent == null || currentPath == null || currentPath.Count == 0) yield break;
        moving = true;

        // 从起点最近点开始（避免瞬移）
        int idx = 0;
        float best = float.MaxValue;
        for (int i = 0; i < currentPath.Count; i++)
        {
            float d = Vector2.SqrMagnitude((Vector2)agent.position - currentPath[i].worldPos);
            if (d < best) { best = d; idx = i; }
        }

        for (int i = idx; i < currentPath.Count; i++)
        {
            Vector2 target = currentPath[i].worldPos;
            while (Vector2.Distance(agent.position, target) > waypointTolerance)
            {
                agent.position = Vector2.MoveTowards(agent.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }
        moving = false;
    }

    // ======== A* 主体 ========
    class PathNode : IHeapItem<PathNode>
    {
        public Node n;
        public int gCost, hCost;
        public PathNode parent;
        public int HeapIndex { get; set; }

        public int fCost => gCost + hCost;

        public int CompareTo(PathNode other)
        {
            int comp = fCost.CompareTo(other.fCost);
            if (comp == 0) comp = hCost.CompareTo(other.hCost);
            return -comp; // 小值优先 -> 返回相反
        }
    }

    List<Node> FindPath(Node start, Node target, bool allowDiagonalMove)
    {
        if (!start.walkable || !target.walkable) { Debug.LogWarning("起点或终点不可达"); return new List<Node>(); }

        var open = new Heap<PathNode>(grid.MaxSize);
        var map = new Dictionary<Node, PathNode>(grid.MaxSize);
        var closed = new HashSet<Node>();

        PathNode GetOrAdd(Node n)
        {
            if (!map.TryGetValue(n, out var pn))
            {
                pn = new PathNode { n = n, gCost = int.MaxValue, hCost = 0, parent = null };
                map[n] = pn;
            }
            return pn;
        }

        var startPN = GetOrAdd(start);
        startPN.gCost = 0;
        startPN.hCost = Heuristic(start, target);
        open.Add(startPN);

        while (open.Count > 0)
        {
            var current = open.RemoveFirst();
            if (current.n == target)
                return RetracePath(current);

            closed.Add(current.n);

            foreach (var neighbour in grid.GetNeighbours(current.n, allowDiagonalMove))
            {
                if (!neighbour.walkable || closed.Contains(neighbour)) continue;

                int newCost = current.gCost + Distance(current.n, neighbour);
                var neighPN = GetOrAdd(neighbour);

                if (newCost < neighPN.gCost)
                {
                    neighPN.gCost = newCost;
                    neighPN.hCost = Heuristic(neighbour, target);
                    neighPN.parent = current;

                    if (!open.Contains(neighPN)) open.Add(neighPN);
                    else open.UpdateItem(neighPN);
                }
            }
        }
        Debug.LogWarning("未找到路径");
        return new List<Node>();
    }

    int Heuristic(Node a, Node b) => Distance(a, b);

    int Distance(Node a, Node b)
    {
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstY = Mathf.Abs(a.gridY - b.gridY);
        // 对角=14，直=10 的常见“八方向”代价（缩放避免浮点）
        return 14 * Mathf.Min(dstX, dstY) + 10 * Mathf.Abs(dstX - dstY);
    }

    List<Node> RetracePath(PathNode end)
    {
        var path = new List<Node>();
        var cur = end;
        while (cur != null) { path.Add(cur.n); cur = cur.parent; }
        path.Reverse();
        return path;
    }
}
