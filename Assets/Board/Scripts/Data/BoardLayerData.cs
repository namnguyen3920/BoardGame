using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BoardGame/Board Layer Data", fileName = "BoardLayerData")]
public class BoardLayerData : ScriptableObject
{
    public TilePalette Palette;
    public List<HexCell> Cells = new List<HexCell>();

    public int Count => Cells != null ? Cells.Count : 0;

    public int IndexOf(int q, int r)
    {
        if (Cells == null) return -1;
        for (int i = 0; i < Cells.Count; i++)
            if (Cells[i].q == q && Cells[i].r == r) return i;
        return -1;
    }

    public bool Contains(int q, int r) => IndexOf(q, r) >= 0;

    public void Set(int q, int r, short value, byte rotation = 0)
    {
        int idx = IndexOf(q, r);
        if (idx >= 0)
        {
            HexCell c = Cells[idx];
            c.value    = value;
            c.rotation = rotation;
            Cells[idx] = c;
        }
        else
        {
            if (Cells == null) Cells = new List<HexCell>();
            Cells.Add(new HexCell(q, r, value, rotation));
        }
    }

    public bool RemoveAt(int q, int r)
    {
        int idx = IndexOf(q, r);
        if (idx < 0) return false;
        Cells.RemoveAt(idx);
        return true;
    }

    public void Clear()
    {
        Cells?.Clear();
    }
}
