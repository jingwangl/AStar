using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundToGrid : MonoBehaviour
{
    public GridGraph2D grid;            // 拖你的 GridGraph2D
    public Color color = new Color(0.11f, 0.12f, 0.15f, 1f); // 背景色
    public int orderInLayer = -10;      // 确保在墙体后面

    static Sprite _fallbackWhite;
    static Sprite FallbackWhite() {
        if (_fallbackWhite) return _fallbackWhite;
        var tex = new Texture2D(1,1,TextureFormat.RGBA32,false);
        tex.SetPixel(0,0,Color.white);
        tex.Apply();
        _fallbackWhite = Sprite.Create(tex, new Rect(0,0,1,1), new Vector2(0.5f,0.5f), 1f);
        _fallbackWhite.name = "BG_FallbackWhite_1x1";
        return _fallbackWhite;
    }

    void Reset() { if (!grid) grid = FindObjectOfType<GridGraph2D>(); Fit(); }
    void OnEnable() { Fit(); }
#if UNITY_EDITOR
    void OnValidate() { Fit(); }
#endif

    [ContextMenu("Fit To Grid")]
    public void Fit()
    {
        if (!grid) return;
        var sr = GetComponent<SpriteRenderer>();
        if (!sr.sprite) sr.sprite = FallbackWhite();

        // 放到迷宫中心，Z 稍小于墙（或用排序层控制）
        var center = grid.transform.position;
        transform.position = new Vector3(center.x, center.y, 0f);

        // 计算缩放：把 1×1 的白图放大到 Grid 的世界尺寸
        var size = grid.gridWorldSize;
        var spSize = sr.sprite.bounds.size; // 一般为 1×1
        float sx = size.x / Mathf.Max(0.0001f, spSize.x);
        float sy = size.y / Mathf.Max(0.0001f, spSize.y);
        transform.localScale = new Vector3(sx, sy, 1f);

        sr.color = color;
        sr.sortingOrder = orderInLayer; // 在墙体后面
    }
}
