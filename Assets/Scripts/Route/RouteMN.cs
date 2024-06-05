using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Enum = System.Enum;
using Unity.VisualScripting;

public class RouteMN : Singleton_Mono_Method<RouteMN>
{
    [Header("Constant variable for special node")]
    public const int MaxSpecialNodes = 6;
    public const int MaxSpecialNodesEnd = 5;
    public const int SpecialNodesStarter = 5;


    [Header("Node data-structure management")]
    Node[] childNodes;
    public List<Node> childNodeList = new List<Node>();
    public List<Node> specialNodeList = new List<Node>();   


    private void Start()
    {
        FillNode();
        RandomSpecialNode(specialNodeList);
        SetNodeProperties();
        SetNodeType();
    }
    public void RandomSpecialNode(List<Node> NodeList)
    {
        int i = 1;

        while (i <= MaxSpecialNodes)
        {
            bool duplicated = false;
            int elems;
            do{
                elems = Random.Range(SpecialNodesStarter, childNodeList.Count - MaxSpecialNodesEnd);
                duplicated = NodeList.Any(c => c.GetComponent<Node>().GetNodeID() == elems);
            } while (duplicated);

            Node specialNode = childNodeList.Find(t => t.GetComponent<Node>().GetNodeID() == elems);

            if (specialNode != null && !duplicated)
            {
                NodeList.Add(specialNode);
            }
            i++;
        }
    }
    void FillNode()
    {
        childNodeList.Clear();

        childNodes = GetComponentsInChildren<Node>();

        int num = -1;
        foreach(Node child in childNodes)
        {
            if(child != this.transform && child != null)
            {
                num++;
                childNodeList.Add(child);
                child.gameObject.name = "Node " + num;
                child.SetNodeId(num);
            }
        }
    }
    public void SetNodeProperties()
    {
        foreach (Node child in childNodeList)
        {
            foreach (Node node in specialNodeList)
            {
                if (node.GetNodeID() == child.GetNodeID())
                {
                    node.isSpecialNode = true;
                }
            }
        }
    }
    public void SetNodeType()
    {
        foreach(Node n_type in childNodeList) 
        { 
            if (n_type.isSpecialNode)
            {
                NodeType randomType = (NodeType)Enum.GetValues(typeof(NodeType))
                    .GetValue(Random.Range(1, Enum.GetValues(typeof(NodeType)).Length));
                n_type.type = randomType;
            }
            else { n_type.type = NodeType.Normal; }
            SpecialNodeMN.d_Instance.AssignMaterial();
        }
    }

    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        FillNode();

        for (int i = 0; i < childNodeList.Count; i++)
        {
            Transform t_node = childNodeList[i].GetComponentInChildren<Transform>();
            Vector3 currentPost = t_node.position;
            if (i > 0)
            {
                Transform t_node_prv = childNodeList[i-1].GetComponentInChildren<Transform>();
                Vector3 previoustPos = t_node_prv.position;
                Gizmos.DrawLine(currentPost, previoustPos);
            }
        }
    }
}
