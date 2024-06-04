using UnityEngine;

public class Bonus : MonoBehaviour, ISpecialNode
{
    public void DoEffect()
    {
        BonusNodeLogic();
    }

    private void BonusNodeLogic()
    {
        Debug.Log("Bonus Node");
        
    }
}
