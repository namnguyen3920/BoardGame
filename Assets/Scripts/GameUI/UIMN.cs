using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMN : Singleton_Mono_Method<UIMN>
{
    [SerializeField] TextMeshProUGUI DiceNumber;
    [SerializeField] Button RollButton;
    int number;

    private void Start()
    {
        
    }
    public void ActiveRollButton(bool status)
    {
        RollButton.interactable = status;
    }
    public void UpdatingRolling(int dice_number)
    {
        number = dice_number;
        string number_text = number.ToString();
        DiceNumber.text = number_text;
    }
}
