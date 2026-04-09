using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : Singleton_Mono_Method<MainMenu>
{
    [Header("For Background")]
    [SerializeField] RawImage img;
    [SerializeField] float x, y;

    [Header("For Game Mode Settings")]
    [SerializeField] public GameObject MainMenuPanel;
    [SerializeField] GameObject mmContent;
    [SerializeField] GameObject mmMode;
    private void Update()
    {
        ScrollImg();
    }

    public void ScrollImg()
    {
        img.uvRect = new Rect(img.uvRect.position + new Vector2(x, y) * Time.deltaTime, img.uvRect.size);
    }

    //Button
    public void StartGameBtn()
    {
        SetStatusMainMenu(false);
        SetStatusModeSettings(true);
    }
    public void QuitGameBtn()
    {
        Application.Quit();
    }
    public void PvPModeBtn()
    {
        foreach(var player in GameMN.d_Instance.playerList)
        {
            player.playerType = GameMN.Player.PlayerType.PLAYER;
        }
        SetStatusMainMenuPanel(false);
    }
    public void BvBModeBtn()
    {
        foreach (var player in GameMN.d_Instance.playerList)
        {
            player.playerType = GameMN.Player.PlayerType.BOT;
        }
        SetStatusMainMenuPanel(false);
    }

    //Updating Status
    void SetStatusMainMenu(bool status)
    {
        mmContent.SetActive(status);
    }
    void SetStatusModeSettings(bool status)
    {
        mmMode.SetActive(status);
    }
    public void SetStatusMainMenuPanel(bool status)
    {
        MainMenuPanel.SetActive(status);
    }
}
