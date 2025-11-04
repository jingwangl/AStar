using System.Collections.Generic;
using UnityEngine;

// 搜索过程可视化：使用 Gizmos 根据节点状态着色
public class SearchVisualizer2D : MonoBehaviour
{
    // 节点显示状态
    public enum NodeVisState { None, Open, Closed, Current, Path, Start, Target }

    [Header("Colors")]
    public Color openColor = new Color(0.2f, 0.6f, 1f, 0.35f);
    public Color closedColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
    public Color currentColor = new Color(1f, 0.85f, 0.2f, 0.55f);
    public Color pathColor = new Color(0.2f, 1f, 0.4f, 0.55f);
    public Color startColor = new Color(0.2f, 1f, 0.2f, 0.6f);
    public Color targetColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("Draw")]
    [Tooltip("绘制方块的尺寸（与网格单元相近）")]
    public Vector2 gizmoSize = new Vector2(0.9f, 0.9f);
    [Tooltip("是否仅在播放模式绘制")]
    public bool onlyInPlayMode = false;

    // 节点状态表
    readonly Dictionary<Node, NodeVisState> nodeStates = new Dictionary<Node, NodeVisState>();

    // 设置节点状态
    public void SetState(Node node, NodeVisState state)
    {
        if (node == null) return;
        if (state == NodeVisState.None)
        {
            nodeStates.Remove(node);
            return;
        }
        nodeStates[node] = state;
    }

    // 高亮起止点
    public void HighlightStartTarget(Node start, Node target)
    {
        if (start != null) SetState(start, NodeVisState.Start);
        if (target != null) SetState(target, NodeVisState.Target);
    }

    // 批量设置路径节点
    public void SetPath(IEnumerable<Node> path)
    {
        if (path == null) return;
        foreach (var n in path)
        {
            if (n == null) continue;
            SetState(n, NodeVisState.Path);
        }
    }

    // 清理所有状态
    public void ClearAll()
    {
        nodeStates.Clear();
    }

    void OnDrawGizmos()
    {
        if (onlyInPlayMode && !Application.isPlaying) return;
        if (nodeStates.Count == 0) return;

        foreach (var kv in nodeStates)
        {
            var node = kv.Key;
            var state = kv.Value;
            Gizmos.color = GetColor(state);

            // Node.worldPos 是 Vector2，这里转为 Vector3 并绘制小方块
            Vector3 pos = new Vector3(node.worldPos.x, node.worldPos.y, 0f);
            Gizmos.DrawCube(pos, new Vector3(gizmoSize.x, gizmoSize.y, 0.01f));
        }
    }

    Color GetColor(NodeVisState state)
    {
        switch (state)
        {
            case NodeVisState.Open: return openColor;
            case NodeVisState.Closed: return closedColor;
            case NodeVisState.Current: return currentColor;
            case NodeVisState.Path: return pathColor;
            case NodeVisState.Start: return startColor;
            case NodeVisState.Target: return targetColor;
            default: return new Color(0f, 0f, 0f, 0f);
        }
    }
}


