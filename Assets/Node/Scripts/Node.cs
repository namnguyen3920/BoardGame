using UnityEngine;
using UnityEngine.UI;

public class Node : MonoBehaviour
{
    private int nodeId;
    public Text numberText;
    public bool isSpecialNode = false;
    public NodeType type;
    public void SetNodeId(int d_nodeId)
    {
        nodeId = d_nodeId;
        if(numberText != null)
        {
            numberText.text = nodeId.ToString();
        }
    }
    public int GetNodeID()
    {
        return nodeId;
    }
}
