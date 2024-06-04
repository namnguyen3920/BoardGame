using UnityEngine;

public abstract class PlayerController : Singleton_Mono_Method<PlayerController>
{    
    [SerializeField] protected RouteMN route;
    [SerializeField] MeshFilter mesh;
    //[SerializeField] protected Player player;
    protected SpecialNodeCreator factory = new SpecialNodeCreator();
}
