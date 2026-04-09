using UnityEngine;

public class BoardSpawner : MonoBehaviour
{
    public BoardData data;
    public TilePalette palette;

    [Min(0.01f)] public float hexSize = 1f;
    public bool flatTop = true;
    public Vector3 tileRotationOffset = new Vector3(0f, 30f, 0f);

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();

        if (data == null || data.cells == null || palette == null) return;

        foreach (HexCell cell in data.cells)
        {
            GameObject prefab = palette.Get(cell.value);
            if (prefab == null) continue;

            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
                if (go == null) go = Instantiate(prefab, transform);
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Spawn Board Tile");
            }
            else
#endif
            {
                go = Instantiate(prefab, transform);
            }

            go.transform.localPosition = AxialToLocal(cell.q, cell.r);
            go.transform.localRotation = prefab.transform.localRotation * Quaternion.Euler(tileRotationOffset);
            go.name = $"Tile_{cell.q}_{cell.r}_v{cell.value}";
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(child);
                continue;
            }
#endif
            Destroy(child);
        }
    }

    public Vector3 AxialToLocal(int q, int r)
    {
        if (flatTop)
        {
            float x = hexSize * 1.5f * q;
            float z = hexSize * Mathf.Sqrt(3f) * (r + q * 0.5f);
            return new Vector3(x, 0f, z);
        }
        else
        {
            float x = hexSize * Mathf.Sqrt(3f) * (q + r * 0.5f);
            float z = hexSize * 1.5f * r;
            return new Vector3(x, 0f, z);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null || data.cells == null) return;
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.5f);
        foreach (HexCell cell in data.cells)
        {
            Vector3 p = transform.TransformPoint(AxialToLocal(cell.q, cell.r));
            Gizmos.DrawWireSphere(p, hexSize * 0.4f);
        }
    }
}
