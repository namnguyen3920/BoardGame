using System;
using System.Collections.Generic;

public class RouteSystem
{
    private readonly List<NodeEntry> _nodes;
    private readonly Dictionary<int, NodeEntry> _indexMap;

    public event Action<int> OnNodeReached;

    public RouteSystem(List<NodeEntry> nodes)
    {
        _nodes = nodes ?? new List<NodeEntry>();
        _indexMap = new Dictionary<int, NodeEntry>(_nodes.Count);
        Rebuild();
    }

    public void Rebuild()
    {
        _indexMap.Clear();
        for (int i = 0; i < _nodes.Count; i++)
            _indexMap[_nodes[i].Index] = _nodes[i];
    }

    public bool IsValidIndex(int index) => _indexMap.ContainsKey(index);

    public bool TryGetNode(int index, out NodeEntry entry) =>
        _indexMap.TryGetValue(index, out entry);

    public int[] GetNextNodes(int currentIndex)
    {
        if (!_indexMap.TryGetValue(currentIndex, out NodeEntry entry))
            return Array.Empty<int>();
        return entry.NextIndices ?? Array.Empty<int>();
    }

    public bool IsTerminal(int index)
    {
        if (!_indexMap.TryGetValue(index, out NodeEntry entry)) return true;
        return entry.NextIndices == null || entry.NextIndices.Length == 0;
    }

    public void NotifyNodeReached(int index) => OnNodeReached?.Invoke(index);

    public List<string> Validate()
    {
        var errors = new List<string>();

        if (_nodes == null || _nodes.Count == 0)
        {
            errors.Add("Route has no nodes.");
            return errors;
        }

        if (!_indexMap.ContainsKey(0))
            errors.Add("No start node (Index 0) found.");

        var seenIndices = new HashSet<int>();
        foreach (NodeEntry node in _nodes)
        {
            if (!seenIndices.Add(node.Index))
                errors.Add($"Duplicate node index: {node.Index}.");
        }

        var seenHex = new Dictionary<(int, int), int>();
        foreach (NodeEntry node in _nodes)
        {
            var key = (node.Q, node.R);
            if (seenHex.TryGetValue(key, out int otherIndex))
                errors.Add($"Nodes {otherIndex} and {node.Index} share hex cell ({node.Q},{node.R}).");
            else
                seenHex[key] = node.Index;
        }

        foreach (NodeEntry node in _nodes)
        {
            if (node.NextIndices == null) continue;
            foreach (int next in node.NextIndices)
            {
                if (next == node.Index)
                    errors.Add($"Node {node.Index} references itself in NextIndices.");
                else if (!_indexMap.ContainsKey(next))
                    errors.Add($"Node {node.Index}: NextIndex {next} references a missing node.");
            }
        }

        if (_indexMap.ContainsKey(0))
        {
            var reachable = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(0);

            while (stack.Count > 0)
            {
                int current = stack.Pop();
                if (!reachable.Add(current)) continue;
                if (!_indexMap.TryGetValue(current, out NodeEntry entry)) continue;
                if (entry.NextIndices == null) continue;

                foreach (int next in entry.NextIndices)
                {
                    if (!reachable.Contains(next))
                        stack.Push(next);
                }
            }

            foreach (NodeEntry node in _nodes)
            {
                if (!reachable.Contains(node.Index))
                    errors.Add($"Node {node.Index} is unreachable from node 0.");
            }
        }

        return errors;
    }
}
