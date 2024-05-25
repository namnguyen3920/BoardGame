using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RouteMN : Singleton_Mono_Method<RouteMN>
{
    Transform[] childObjects;
    public List<Transform> childNodeList = new List<Transform>();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        FillNode();

        for(int i = 0; i< childNodeList.Count; i++)
        {
            Vector3 currentPost = childNodeList[i].position;
            if(i>0)
            {
                Vector3 previoustPos = childNodeList[i - 1].position;
                Gizmos.DrawLine(currentPost, previoustPos);
            }
        }
    }

    void FillNode()
    {
        childNodeList.Clear();

        childObjects = GetComponentsInChildren<Transform>();

        foreach(Transform child in childObjects)
        {
            if(child != this.transform)
            {
                childNodeList.Add(child);
            }
        }
    }
}
