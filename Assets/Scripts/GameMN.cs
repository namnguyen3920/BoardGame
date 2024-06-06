using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum States
{
    Waiting,
    RollDice,
    SwitchPlayer
}
public class GameMN : Singleton_Mono_Method<GameMN>
{
    public List<Player> playerList = new();
    public List<Player> playerRanking = new();
    int activePlayer;
    int dice_number;

    public States state;

    private void FixedUpdate()
    {
        if(playerList.Count != 0)
        {
            GameStatus();
        }        
    }

    IEnumerator RollDiceDelay()
    {
        yield return new WaitForSeconds(0.5f);
        dice_number = DiceMN.d_Instance.Roll();
        UIMN.d_Instance.UpdatingRolling(dice_number);
        
        playerList[activePlayer].turns++;
        playerList[activePlayer].main.MakeTurn(dice_number);
        UIMN.d_Instance.ShowTurns(playerList[activePlayer].playerName);
    }

    public void PlayerRollDice()
    {
        if (playerList == null) { return; }
        else
        {
            UIMN.d_Instance.ActiveRollButton(false);
            StartCoroutine(RollDiceDelay());
        }
    }
    public IEnumerator ReportWinner()
    {
        playerList[activePlayer].rank = playerRanking.Count+1;
        RankingPlayer(playerList[activePlayer]);
        if (playerList.Count == 0)
        {
            UIMN.d_Instance.SetStatusEGPanel(true);
            UIMN.d_Instance.SetStatusGOPanel(true);
            UIMN.d_Instance.ShowWinMessage(playerRanking[0].playerName);
            foreach (Player player in playerRanking)
            {
                RectTransform playerStats = Instantiate(UIMN.d_Instance.PlayerStatsContent);
                UIMN.d_Instance.UpdatingStatic(player.rank, player.playerName, player.turns, player.bonusSteps, player.failSteps);
                yield return new WaitForSeconds(0.5f);
                playerStats.transform.SetParent(UIMN.d_Instance.ScrollViewContent, false);
                UIMN.d_Instance.PlayerStatsContent.gameObject.SetActive(true);
            }
        }
    }
    void RankingPlayer(Player player)
    {
        playerRanking.Add(player);
        playerList.Remove(player);
    }
    public void SetFailCounter(int fail_number) { playerList[activePlayer].failSteps = fail_number; }
    public void SetBonusCounter(int bonus_number) { playerList[activePlayer].bonusSteps = bonus_number; }
    void SwitchCondition()
    {
        if(playerList.Count == 1)
        {
            activePlayer = 0;
        }
        else
        {
            activePlayer++;
            activePlayer %= playerList.Count;
        }        
        state = States.RollDice;
    }
    void SwitchConditionForBot()
    {
        if (playerList.Count > 1)
        {
            Debug.Log("Bor called for another bot");
            activePlayer++;
            activePlayer %= playerList.Count;
        }
        state = States.RollDice;
    }
    void GameStatus()
    {
        if (playerList[activePlayer].playerType == Player.PlayerType.BOT)
            {
                switch (state)
                {
                    case States.Waiting:
                        break;
                    case States.RollDice:
                        {
                            PlayerRollDice();
                            state = States.Waiting;
                    }
                        break;
                    case States.SwitchPlayer:
                        {
                            SwitchCondition();
                            state = States.RollDice;
                    }
                        break;
                    default: break;
                }
            }

        if (playerList[activePlayer].playerType == Player.PlayerType.PLAYER)
            {
                switch (state)
                {
                    case States.Waiting:
                        break;
                    case States.RollDice:
                        {
                            UIMN.d_Instance.ActiveRollButton(true);
                            state = States.Waiting;
                    }
                        break;
                    case States.SwitchPlayer:
                        {
                            SwitchCondition();
                            state = States.RollDice;
                    }
                        break;
                    default:
                        break;
                }
            }
    }

    [System.Serializable]
    public class Player
    {
        public string playerName;
        public MainPlayer main;
        public PlayerType playerType;
        public int failSteps;
        public int bonusSteps;
        public int turns;
        public int rank;
        public float score;
        public enum PlayerType
        {
            PLAYER,
            BOT
        }
    }

}
