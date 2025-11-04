using System.Collections.Generic;
using UnityEngine;

public class PathfindingAStar : MonoBehaviour
{
    [Header("Refs")]
    public GridGraph2D grid;            // 拖拽 GridGraph2D
    public Transform agent;             // 你的小球
    public LineRenderer line;           // 用于显示路径（可空）
    

    [Header("Move")]
    public float moveSpeed = 4f;
    public float waypointTolerance = 0.05f;

    [Header("Visualization")]
    public bool visualizeSearch = false;            // 是否可视化搜索过程
    public int stepsPerFrame = 1;                   // 每帧推进多少个搜索步骤
    public float stepDelay = 0.02f;                 // 每批步骤后延迟，0 为不延迟
    public SearchVisualizer2D visualizer;           // 可视化组件（可空）

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

        // 开启新一轮寻路：先停止上一轮所有协程并清空可视化与路径线
        StopAllCoroutines();
        if (visualizer != null) visualizer.ClearAll();
        if (line != null) ApplyLineRenderer(null);
        if (grid != null) grid.SetDebugPath(new List<Node>()); // 清空网格调试路径
        if (visualizer != null) visualizer.HighlightStartTarget(startNode, targetNode);

        if (visualizeSearch)
        {
            // 可视化逐步搜索（仅四方向）
            StartCoroutine(FindAndFollowPathVisual(startNode, targetNode));
            return;
        }

        var oldPath = currentPath;
        var newPath = FindPath(startNode, targetNode);

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
            return comp; // 小值优先 -> 直接按升序
        }
    }

    List<Node> FindPath(Node start, Node target)
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

            foreach (var neighbour in grid.GetNeighbours(current.n))
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
        // 曼哈顿距离（四方向）：每步代价 10
        return 10 * (dstX + dstY);
    }

    List<Node> RetracePath(PathNode end)
    {
        var path = new List<Node>();
        var cur = end;
        while (cur != null) { path.Add(cur.n); cur = cur.parent; }
        path.Reverse();
        return path;
    }

    // ======== 逐步可视化版 A*（协程） ========
    System.Collections.IEnumerator FindAndFollowPathVisual(Node start, Node target)
    {
        // 清理可视化状态
        if (visualizer != null)
        {
            visualizer.ClearAll();
            visualizer.HighlightStartTarget(start, target);
        }

        if (!start.walkable || !target.walkable)
        {
            Debug.LogWarning("起点或终点不可达");
            yield break;
        }

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
        if (visualizer != null) visualizer.SetState(start, SearchVisualizer2D.NodeVisState.Open);

        int stepCounter = 0;
        bool found = false;
        PathNode endPN = null;

        while (open.Count > 0)
        {
            var current = open.RemoveFirst();
            if (visualizer != null) visualizer.SetState(current.n, SearchVisualizer2D.NodeVisState.Current);

            // 步进节流
            if (++stepCounter % Mathf.Max(1, stepsPerFrame) == 0)
            {
                if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay); else yield return null;
            }

            if (current.n == target)
            {
                found = true;
                endPN = current;
                break;
            }

            closed.Add(current.n);

            foreach (var neighbour in grid.GetNeighbours(current.n))
            {
                if (!neighbour.walkable || closed.Contains(neighbour)) continue;

                int newCost = current.gCost + Distance(current.n, neighbour);
                var neighPN = GetOrAdd(neighbour);

                if (newCost < neighPN.gCost)
                {
                    neighPN.gCost = newCost;
                    neighPN.hCost = Heuristic(neighbour, target);
                    neighPN.parent = current;

                    if (!open.Contains(neighPN)) open.Add(neighPN); else open.UpdateItem(neighPN);
                    if (visualizer != null) visualizer.SetState(neighbour, SearchVisualizer2D.NodeVisState.Open);

                    // 步进节流
                    if (++stepCounter % Mathf.Max(1, stepsPerFrame) == 0)
                    {
                        if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay); else yield return null;
                    }
                }
            }

            if (visualizer != null) visualizer.SetState(current.n, SearchVisualizer2D.NodeVisState.Closed);
        }

        if (!found)
        {
            Debug.LogWarning("未找到路径");
            yield break;
        }

        var path = RetracePath(endPN);
        currentPath = path;

        if (visualizer != null) visualizer.SetPath(path);
        grid.SetDebugPath(path);
        ApplyLineRenderer(path);

        moving = false;
        StopAllCoroutines();
        StartCoroutine(FollowPath());
    }
}
