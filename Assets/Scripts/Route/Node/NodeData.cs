using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public abstract class NodeData : MonoBehaviour
{
    public int nodeId;
    public Text numberText;
    public bool isSpecialNode = false;
    public NodeType nodeType;

    public abstract void SetNodeId(int d_nodeId);

}


