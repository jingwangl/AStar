using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 墙体编辑器：支持手动放置与移除墙体
/// 操作：
///   - 左键：在光标所在网格位置生成墙体
///   - 按下 C：清空所有墙体
/// </summary>
public class WallEditor : MonoBehaviour
{
    [Header("引用")]
    public GridGraph2D grid;                 // 生成墙体后需要重建网格
    public Transform obstaclesRoot;          // 墙体的父节点

    [Header("墙体设置")]
    public Sprite wallSprite;                // 墙体精灵
    public Vector2 wallTilePadding = new Vector2(0.95f, 0.95f); // 墙体相对格子的缩放
    public string wallLayerName = "";        // 可选：墙体所在图层
    public PhysicsMaterial2D wallPhysicsMaterial; // 可选：墙体碰撞材质

    [Header("操作")]
    public bool enableEditing = true;        // 是否允许编辑

    float nodeDiameter;
    List<GameObject> createdWalls = new List<GameObject>();

    void Reset()
    {
        grid = GetComponent<GridGraph2D>();
    }

    void Update()
    {
        if (!enableEditing) return;

        // 左键添加墙体
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 clickPos = ScreenToWorld(Input.mousePosition);
            AddWallAtPosition(clickPos);
        }

        // C 键清空墙体
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllWalls();
        }
    }

    Vector2 ScreenToWorld(Vector3 mousePos)
    {
        var worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        return new Vector2(worldPos.x, worldPos.y);
    }

    void AddWallAtPosition(Vector2 worldPos)
    {
        if (grid == null)
        {
            Debug.LogWarning("[WallEditor] 未指定 GridGraph2D。");
            return;
        }

        if (obstaclesRoot == null)
        {
            Debug.LogWarning("[WallEditor] 未指定墙体父节点。");
            return;
        }

        // 定位点击处所属的节点
        Node targetNode = grid.NodeFromWorldPoint(worldPos);
        if (targetNode == null)
        {
            Debug.LogWarning("[WallEditor] 无法找到点击位置对应的节点。");
            return;
        }

        // 避免重复生成同一位置的墙体
        Vector2 wallPos = targetNode.worldPos;
        foreach (var wall in createdWalls)
        {
            if (wall != null && Vector2.Distance(wall.transform.position, wallPos) < 0.01f)
            {
                Debug.Log("[WallEditor] 该位置已经存在墙体。");
                return;
            }
        }

        // 生成墙体
        nodeDiameter = grid.nodeRadius * 2f;
        GameObject wallObj = CreateWallObject(wallPos);
        createdWalls.Add(wallObj);

        // 直接标记网格为阻塞（不再依赖物理碰撞体采样）
        grid.BlockNode(targetNode);

        Debug.Log($"[WallEditor] 已在 {wallPos} 生成墙体");
    }

    GameObject CreateWallObject(Vector2 position)
    {
        var go = new GameObject($"Wall_Manual_{createdWalls.Count}");
        go.transform.SetParent(obstaclesRoot, worldPositionStays: false);
        go.transform.position = position;

        // 可选：设置层级
        if (!string.IsNullOrEmpty(wallLayerName))
        {
            int wallLayer = LayerMask.NameToLayer(wallLayerName);
            if (wallLayer != -1) go.layer = wallLayer;
            else Debug.LogWarning($"[WallEditor] 图层 '{wallLayerName}' 不存在，将沿用默认图层。");
        }

        // 添加精灵渲染
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = wallSprite;
        
        if (sr.sprite != null)
        {
            float sx = nodeDiameter * wallTilePadding.x;
            float sy = nodeDiameter * wallTilePadding.y;
            go.transform.localScale = new Vector3(
                sx / sr.sprite.bounds.size.x,
                sy / sr.sprite.bounds.size.y,
                1f);
        }
        else
        {
            // 没有指定精灵时使用简单缩放
            go.transform.localScale = new Vector3(
                nodeDiameter * wallTilePadding.x,
                nodeDiameter * wallTilePadding.y,
                1f);
        }

        // 添加碰撞体
        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(nodeDiameter * wallTilePadding.x, nodeDiameter * wallTilePadding.y);
        bc.sharedMaterial = wallPhysicsMaterial;

        return go;
    }

    public void ClearAllWalls()
    {
        if (obstaclesRoot == null)
        {
            Debug.LogWarning("[WallEditor] 未指定墙体父节点。");
            return;
        }

        // 清除列表中缓存的墙体
        foreach (var wall in createdWalls)
        {
            if (wall != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(wall);
                else Destroy(wall);
#else
                Destroy(wall);
#endif
            }
        }
        createdWalls.Clear();

        // 清空父节点下的所有墙体（包括迷宫生成的）
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in obstaclesRoot)
        {
            toDestroy.Add(child.gameObject);
        }

        foreach (var obj in toDestroy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(obj);
            else Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        // 取消所有阻塞
        if (grid != null)
        {
            grid.ClearAllBlocks();
        }

        Debug.Log("[WallEditor] 已清空所有墙体。");
    }

    [ContextMenu("清空所有墙体")]
    void ClearAllWallsMenuItem()
    {
        ClearAllWalls();
    }
}

