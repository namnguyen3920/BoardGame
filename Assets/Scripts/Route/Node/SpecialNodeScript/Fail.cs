
using System.Collections;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using static GameMN;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class Fail : MonoBehaviour, ISpecialNode
{
    int backward_steps = 3;
    int f_counter = 0;
    MainPlayer player;
    public void DoEffect()
    {
        FailNodeLogic();
    }
    private void FailNodeLogic()
    {
        f_counter++;
        if (player != null)
        {
            Debug.Log("Fail Node");
            //player.GetMovingScript(backward_steps);
        }
        else
        {
            Debug.Log("Player is null");
        }
    }

    public int GetCounter()
    {
        return f_counter;
    }
}
