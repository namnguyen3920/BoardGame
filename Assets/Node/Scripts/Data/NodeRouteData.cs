using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BoardGame/Node Route", fileName = "NodeRouteData")]
public class NodeRouteData : ScriptableObject
{
    public List<NodeEntry> Nodes = new List<NodeEntry>();

    public int Count => Nodes != null ? Nodes.Count : 0;

    public bool TryGetNode(int index, out NodeEntry entry)
    {
        if (Nodes == null)
        {
            entry = default;
            return false;
        }

        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Index == index)
            {
                entry = Nodes[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    public bool IsOccupied(int q, int r)
    {
        if (Nodes == null) return false;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Q == q && Nodes[i].R == r) return true;
        }
        return false;
    }

    public int NextAvailableIndex()
    {
        if (Nodes == null || Nodes.Count == 0) return 0;
        int max = 0;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Index > max) max = Nodes[i].Index;
        }
        return max + 1;
    }
}
