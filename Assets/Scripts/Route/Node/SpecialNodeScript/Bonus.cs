using UnityEngine;

public class Bonus : MonoBehaviour, ISpecialNode
{
    int b_counter = 0;
    public void DoEffect()
    {
        b_counter++;
        BonusNodeLogic();
    }

    private void BonusNodeLogic()
    {
        Debug.Log("Bonus Node");
        //GameMN.d_Instance.state = States.RollDice;
    }
    public int GetCounter()
    {
        return b_counter;
    }
}
