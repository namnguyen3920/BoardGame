using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;


public class PlayerMovement : MonoBehaviour
{
    public RouteMN currentRoute;

    int RoutePos;

    [SerializeField] int steps;
    [SerializeField] bool isMoving;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isMoving)
        {
            steps = Random.Range(1, 6);
            Debug.Log("Dice rolled: " + steps);

            if (RoutePos + steps < currentRoute.childNodeList.Count)
            {
                StartCoroutine(Move());
            }
            else
            {
                Debug.Log("Rolled number is too high.");
            }

        }
    }

    IEnumerator Move()
    {
        if (isMoving)
        {
            yield break;
        }
        isMoving = true;

        while(steps > 0)
        {
            Vector3 nextPos = currentRoute.childNodeList[steps + 1].position;
            while (MoveToNextNode(nextPos)) { yield return null; }

            yield return new WaitForSeconds(0.1f);
            steps--;
            RoutePos++;
        }
        isMoving = false;
    }

    bool MoveToNextNode(Vector3 targetTile)
    {
        return targetTile != (transform.position = Vector3.MoveTowards(transform.position, targetTile, 2f * Time.deltaTime));
    }
    void ArrivedOnTile()
    {
        transform.DOShakeScale(0.5f);
    }
}
