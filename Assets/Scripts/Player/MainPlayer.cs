using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainPlayer : PlayerController
{
    List<Node> nodeList = new List<Node>();
    protected bool isMoving;
    protected int RoutePos;
    int steps;
    int doneSteps;
    [SerializeField] float movingSpeed = 2f;
    
    private void Start()
    {
        nodeList = route.childNodeList;
    }
    public void MakeTurn(int dice_number)
    {
        steps = dice_number;
        if (doneSteps + steps < route.childNodeList.Count)
        {
            StartCoroutine(MovingPlayer());
        }
        else
        {
            GameMN.d_Instance.state = States.SwitchPlayer;
        }
    }
    public IEnumerator MovingPlayer()
    {
        ISpecialNode bonusNode = factory.CreateSpecialNode(NodeType.Bonus);
        ISpecialNode failNode = factory.CreateSpecialNode(NodeType.Fail);

        isMoving = true;

        while (steps > 0)
        {
            RoutePos++;
            Transform nextNode = route.childNodeList[RoutePos].GetComponentInChildren<Transform>();
            Vector3 nextPos = nextNode.position;

            while (MoveToNextNodeTowards(nextPos))
            {
                yield return null;
            }
            steps--;
            doneSteps++;
        }

        if (nodeList[RoutePos].isSpecialNode)
        {
            switch (nodeList[RoutePos].type)
            {
                case NodeType.Fail:
                    {
                        failNode.DoEffect();
                        break;
                    }
                case NodeType.Bonus:
                    {
                        bonusNode.DoEffect();
                        break;
                    }
            }
        }
        yield return new WaitForSeconds(0.1f);

        GameMN.d_Instance.state = States.SwitchPlayer;

        yield return isMoving;
    }

    bool MoveToNextNodeTowards(Vector3 targetNode)
    {
        return targetNode != (transform.position = Vector3.MoveTowards(transform.position, targetNode, movingSpeed * Time.deltaTime));
    }
}
