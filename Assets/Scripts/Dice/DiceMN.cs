using System;
using UnityEngine;

public class DiceMN : Singleton_Mono_Method<DiceMN>
{
    private const int MAXDICE_NUMB = 6;
    int dice_number;
    public bool isDiceRolled;
    
    public int Roll()
    {
        dice_number = UnityEngine.Random.Range(1, MAXDICE_NUMB);
        isDiceRolled = true;
        return dice_number;
    }
}
