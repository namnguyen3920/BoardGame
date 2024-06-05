using System.Collections.Generic;
using TMPro;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameMN;

public class UIMN : Singleton_Mono_Method<UIMN>
{
    [SerializeField] TextMeshProUGUI DiceNumber;
    [SerializeField] Text message;
    [SerializeField] Button RollButton;
    int number;
    
    public void SetActiveGameOverUI(GameObject panel)
    {
        panel.SetActive(true);
    }
    public void ShowWinMessage(string winner)
    {
        message.text = winner + " has won this round!";
    }
    public void BackBtn()
    {
        SceneManager.LoadScene("GamePlay");
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
