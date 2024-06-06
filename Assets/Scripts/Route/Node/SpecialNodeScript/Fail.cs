using UnityEngine;

public class Fail : MonoBehaviour, ISpecialNode
{
    int f_counter = 0;
    MainPlayer player;
    public void DoEffect()
    {
        FailNodeLogic();
    }
    private void FailNodeLogic()
    {
        f_counter++;
    }

    public int GetCounter()
    {
        return f_counter;
    }
}
