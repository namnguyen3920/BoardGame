using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct HexCell
{
    public int q;
    public int r;
    public short value;
    public byte rotation; 

    public HexCell(int q, int r, short value, byte rotation = 0)
    {
        this.q = q;
        this.r = r;
        this.value = value;
        this.rotation = rotation;
    }
}

[CreateAssetMenu(menuName = "BoardGame/Board Data", fileName = "BoardData")]
public class BoardData : ScriptableObject
{
    public List<HexCell> cells = new List<HexCell>();

    public int Count => cells != null ? cells.Count : 0;

    public int IndexOf(int q, int r)
    {
        if (cells == null) return -1;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].q == q && cells[i].r == r) return i;
        }
        return -1;
    }

    public bool Contains(int q, int r) => IndexOf(q, r) >= 0;

    public bool TryGet(int q, int r, out short value)
    {
        int idx = IndexOf(q, r);
        if (idx < 0) { value = 0; return false; }
        value = cells[idx].value;
        return true;
    }

    public void Set(int q, int r, short value, byte rotation = 0)
    {
        int idx = IndexOf(q, r);
        if (idx >= 0)
        {
            HexCell c = cells[idx];
            c.value = value;
            c.rotation = rotation;
            cells[idx] = c;
        }
        else
        {
            if (cells == null) cells = new List<HexCell>();
            cells.Add(new HexCell(q, r, value, rotation));
        }
    }

    public void SetRotation(int q, int r, byte rotation)
    {
        int idx = IndexOf(q, r);
        if (idx < 0) return;
        HexCell c = cells[idx];
        c.rotation = (byte)(rotation % 6);
        cells[idx] = c;
    }

    public bool RemoveAt(int q, int r)
    {
        int idx = IndexOf(q, r);
        if (idx < 0) return false;
        cells.RemoveAt(idx);
        return true;
    }

    public void Clear()
    {
        if (cells != null) cells.Clear();
    }
}
