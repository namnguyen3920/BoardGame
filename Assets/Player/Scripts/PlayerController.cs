using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerController : Singleton_Mono_Method<PlayerController>
{
    [Header("GetComponent Part")]
    [SerializeField] protected RouteMN route;
    [SerializeField] MeshFilter mesh;
    public ISpecialNode BonusNode;
    public ISpecialNode FailNode;
    protected SpecialNodeCreator factory = new SpecialNodeCreator();

    [Header("Variables Part")]
    [SerializeField] int backward_steps = 3;
    public bool isMovingForward = true;
    protected bool isMoving;
    public bool reachDes;
    protected int RoutePos;

    [Header("Data-structure Part")]
    List<Node> nodeList = new List<Node>();

    private void Start()
    {
        nodeList = route.childNodeList;
        BonusNode = factory.CreateSpecialNode(NodeType.Bonus);
        FailNode = factory.CreateSpecialNode(NodeType.Fail);
    }

    public abstract IEnumerator MovingPlayer(int steps);
    public abstract IEnumerator MovingPlayerBackward(int steps);
    protected void CheckingNode()
    {
        switch (nodeList[RoutePos].type)
        {
            case NodeType.Fail:
                {
                    isMovingForward = false;
                    StartCoroutine(MovingPlayerBackward(backward_steps));
                    FailNode.DoEffect();
                    GameMN.d_Instance.SetFailCounter(FailNode.GetCounter());
                    break;
                }
            case NodeType.Bonus:
                {
                    GameMN.d_Instance.state = States.RollDice;
                    BonusNode.DoEffect();
                    GameMN.d_Instance.SetBonusCounter(BonusNode.GetCounter());
                    break;
                }
            default:
                GameMN.d_Instance.state = States.SwitchPlayer;
                break;
        }
    }
}
