using UnityEngine;

public class Node
{
    public bool walkable;
    public Vector2 worldPos;
    public int gridX, gridY;

    public int gCost;   // 起点到当前
    public int hCost;   // 估价：当前到终点
    public Node parent; // 回溯路径

    public Node(bool walkable, Vector2 worldPos, int x, int y)
    {
        this.walkable = walkable;
        this.worldPos = worldPos;
        gridX = x; gridY = y;
    }

    public int fCost => gCost + hCost;
}
