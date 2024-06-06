using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIMN : Singleton_Mono_Method<UIMN>
{
    [Header("In Game Menu")]
    [SerializeField] TextMeshProUGUI DiceNumber;
    [SerializeField] Text winnerMessage;
    [SerializeField] Button RollButton;
    [SerializeField] Text PlayerTurnText;

    [Header("UI Panel")]
    [SerializeField] GameObject EndGamePanel;
    [SerializeField] GameObject EndGameMenu;
    [SerializeField] GameObject StaticMenu;
    [SerializeField] GameObject SettingsMenu;

    [Header("Statistic Component")]
    [SerializeField] Text placeText;
    [SerializeField] Text playerNameText;
    [SerializeField] Text turnText;
    [SerializeField] Text bonusText;
    [SerializeField] Text failText;
    [SerializeField] public RectTransform ScrollViewContent;
    [SerializeField] public RectTransform PlayerStatsContent;

    int number;
    int place;
    int turn;
    int bonus;
    int fail;

    public void Init()
    {
        SetStatusGOPanel(false);
        SetStatusEGPanel(false);
    }
    private void Start()
    {
        Init();
    }


    //ACTIVE PANEL
    public void SetStatusStatisticPanel(bool status)
    {
        StaticMenu.SetActive(status);
    }
    public void SetStatusEGPanel(bool status)
    {
        EndGamePanel.SetActive(status);
    }
    public void SetStatusGOPanel(bool status)
    {
        EndGameMenu.SetActive(status);
    }
    public void SetStatusSettingsMenu(bool status)
    {
        SettingsMenu.SetActive(status);
    }


    //UPDATING DATA
    public void UpdatingStatic(int place_number, string name, int turn_number, int bonus_number, int fail_number)
    {
        //ClearData();
        this.place = place_number;
        this.turn = turn_number;
        this.bonus = bonus_number;
        this.fail = fail_number;

        string place_text = place.ToString();
        string turn_text = turn.ToString();
        string bonus_text = bonus.ToString();
        string fail_text = fail.ToString();

        placeText.text = place_text;
        playerNameText.text = name;
        turnText.text = turn_text;
        bonusText.text = bonus_text;
        failText.text = fail_text;
    }
    public void ShowWinMessage(string winner)
    {
        winnerMessage.text = winner + " has won this round!";
    }
    public void ShowTurns(string player)
    {
        PlayerTurnText.text = player + "'s turn!";
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


    //BUTTON LOGIC
    public void CloseSettingBtn()
    {
        SettingsMenu.SetActive(false);
        SetTimeScale(1);
    }
    public void SettingsBtn()
    {
        SettingsMenu.SetActive(true);
        SetTimeScale(0);
    }
    public void BackToGOBtn()
    {
        SetStatusStatisticPanel(false);
        SetStatusGOPanel(true);
    }
    public void MoveToStaticBtn()
    {
        SetStatusStatisticPanel(true);
        SetStatusGOPanel(false);
    }
    public void RestartBtn()
    {
        SceneManager.LoadScene("GamePlay");
    }
    void SetTimeScale(int speed)
    {
        Time.timeScale = speed;
    }
}
