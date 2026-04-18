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
        this.q        = q;
        this.r        = r;
        this.value    = value;
        this.rotation = rotation;
    }
}

[CreateAssetMenu(menuName = "BoardGame/Board Data", fileName = "BoardData")]
public class BoardData : ScriptableObject
{
    public List<BoardLayerData> layers = new List<BoardLayerData>();
}
