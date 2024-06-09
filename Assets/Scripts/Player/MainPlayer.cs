using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MainPlayer : PlayerController
{    
    int steps;
    int doneSteps = 0;
    [SerializeField] float movingSpeed = 4f;

    public void MakeTurn(int dice_number)
    {
        steps = dice_number;
        if (doneSteps + steps < route.childNodeList.Count)
        {
            isMovingForward = true;
            StartCoroutine(MovingPlayer(steps));
        }
        else
        {
            GameMN.d_Instance.state = States.SwitchPlayer;
        }
    }

    public override IEnumerator MovingPlayerBackward(int back_steps)
    {
        yield return new WaitForSeconds(0.7f);
        reachDes = false;
        isMoving = true;

        while (back_steps > 0)
        {
            RoutePos--;
            Transform nextNode = route.childNodeList[RoutePos].GetComponentInChildren<Transform>();
            Vector3 nextPos = nextNode.position;

            while (MoveToNextNodeTowards(nextPos))
            {
                yield return null;
            }
            back_steps--;
            doneSteps--;
        }

        CheckingNode();

        yield return new WaitForSeconds(0.1f);

        isMoving = false;
        reachDes = true;
    }

    public override IEnumerator MovingPlayer(int steps)
    {
        reachDes = false;
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

        if (doneSteps == route.childNodeList.Count - 1)
        {
            StartCoroutine(GameMN.d_Instance.ReportWinner());
            yield return new WaitForSeconds(0.3f);
        }

        CheckingNode();

        yield return new WaitForSeconds(0.1f);

        isMoving = false;
        reachDes = true;
    }
    bool MoveToNextNodeTowards(Vector3 targetNode)
    {
        return targetNode != (transform.position = Vector3.MoveTowards(transform.position, targetNode, movingSpeed * Time.deltaTime));
    }
}
