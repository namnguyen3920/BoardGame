using System;

public enum NodeType
{
    Normal,
    Bonus,
    Fail
}

[Serializable]
public struct NodeEntry
{
    public int Index;
    public NodeType Type;
    public int Q;
    public int R;
    public int[] NextIndices;
    public string Label;

    public NodeEntry(int index, NodeType type, int q, int r)
    {
        Index = index;
        Type = type;
        Q = q;
        R = r;
        NextIndices = Array.Empty<int>();
        Label = string.Empty;
    }
}
