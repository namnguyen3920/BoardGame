
using UnityEngine;

public class SpecialNodeMN : Singleton_Mono_Method<SpecialNodeMN>
{
    [SerializeField] Material bonusMat;
    [SerializeField] Material failMat;
    [SerializeField] Material default_mat;
    [SerializeField] RouteMN route;
    public MainPlayer player;
    public void AssignMaterial()
    {
        if(route.childNodeList.Count == 0)
        {
            return;
        }

        foreach(Node node in route.childNodeList)
        {
            MeshRenderer meshRend = node.GetComponent<MeshRenderer>();
            switch (node.type)
            {
                case NodeType.Bonus:
                    {
                        meshRend.material = bonusMat; 
                        break;
                    }
                case NodeType.Fail:
                    {
                        meshRend.material = failMat;
                        break;
                    }
                default:
                    {
                        meshRend.material = default_mat;
                        break;
                    }
            }

        }
        
    }


}
