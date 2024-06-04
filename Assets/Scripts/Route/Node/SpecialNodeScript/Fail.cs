
using UnityEngine;

public class Fail : MonoBehaviour, ISpecialNode
{
    public void DoEffect()
    {
        FailNodeLogic();
    }

    private void FailNodeLogic()
    {
        Debug.Log("Fail Node");
    }
}
