using UnityEngine;

// Auto-fit an orthographic camera to fully frame the GridGraph2D area
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraFitToGrid2D : MonoBehaviour
{
    public GridGraph2D grid;        // Target grid to frame
    [Tooltip("Extra world units padding around the grid bounds")]
    public float padding = 0.5f;
    [Tooltip("Follow grid transform position")] 
    public bool followPosition = true;
    [Tooltip("Auto update when columns/rows/position change")] 
    public bool autoUpdate = true;

    Camera cam;
    int lastCols, lastRows;
    Vector3 lastGridPos;

    void Reset()
    {
        grid = FindObjectOfType<GridGraph2D>();
        cam = GetComponent<Camera>();
        if (cam != null) cam.orthographic = true;
        Fit();
    }

    void OnEnable()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null) cam.orthographic = true;
        Fit();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null) cam.orthographic = true;
            Fit();
        }
    }
#endif

    void LateUpdate()
    {
        if (!autoUpdate || grid == null) return;
        if (grid.columns != lastCols || grid.rows != lastRows || (followPosition && grid.transform.position != lastGridPos))
        {
            Fit();
        }
    }

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<GridGraph2D>();
            if (grid == null) return;
        }
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;
        cam.orthographic = true;

        Vector2 size = grid.gridWorldSize; // derived from (columns, rows)
        float width = size.x + padding * 2f;
        float height = size.y + padding * 2f;

        float aspect = cam.aspect > 0f ? cam.aspect : ((float)Screen.width / Mathf.Max(1, Screen.height));
        float halfH = height * 0.5f;
        float halfW = width * 0.5f;
        float sizeToFitWidth = halfW / Mathf.Max(0.0001f, aspect);
        cam.orthographicSize = Mathf.Max(halfH, sizeToFitWidth);

        if (followPosition)
        {
            Vector3 p = grid.transform.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        lastCols = grid.columns;
        lastRows = grid.rows;
        lastGridPos = grid.transform.position;
    }
}


