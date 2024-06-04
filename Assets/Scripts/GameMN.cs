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
    int activePlayer;
    int dice_number;
    public States state;
    private void Update()
    {
        if (playerList[activePlayer].p_type == Player.PlayerType.BOT)
        {
            switch (state)
            {
                case States.Waiting:
                    break;
                case States.RollDice:
                    {
                        StartCoroutine(RollDiceDelay());
                        state = States.Waiting;
                    }
                    break;
                case States.SwitchPlayer:
                    {
                        activePlayer++;
                        activePlayer %= playerList.Count;
                        state = States.RollDice;
                    }
                    break;
                default: break;
            }
        }

        if (playerList[activePlayer].p_type == Player.PlayerType.PLAYER)
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
                        activePlayer++;
                        activePlayer %= playerList.Count;
                        state = States.RollDice;
                    }
                    break;
                default: break;
            }
        }

    }
    IEnumerator RollDiceDelay()
    {
        yield return new WaitForSeconds(1f);
        dice_number = DiceMN.d_Instance.RandomDice();
        UIMN.d_Instance.UpdatingRolling(dice_number);
        //Make turn
        playerList[activePlayer].main.MakeTurn(dice_number);
    }

    public void PlayerRollDice()
    {
        UIMN.d_Instance.ActiveRollButton(false);
        StartCoroutine(RollDiceDelay());
    }

    [System.Serializable]
    public class Player
    {
        public string PlayerName;
        public MainPlayer main;
        public PlayerType p_type;
        public enum PlayerType
        {
            BOT,
            PLAYER
        }
    }

}
