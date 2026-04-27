using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;
using jp.ootr.common;

namespace jp.ootr.ludo
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LudoUIController : BaseClass
    {
        [SerializeField] private LudoGameController controller;

        [SerializeField] private TextMeshProUGUI   turnText;
        [SerializeField] private TextMeshProUGUI   diceText;
        [SerializeField] private TextMeshProUGUI[] playerNameTexts = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] rankTexts       = new TextMeshProUGUI[4];

        [SerializeField] private GameObject   rollButton;
        [SerializeField] private GameObject   startButton;
        [SerializeField] private GameObject[] joinButtons  = new GameObject[4];
        [SerializeField] private GameObject[] leaveButtons = new GameObject[4];

        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject endPanel;

        public void OnStateUpdated(LudoGameController ctrl)
        {
            GamePhase phase = ctrl.GetPhase();

            bool showLobby = phase == GamePhase.Lobby ||
                             phase == GamePhase.Idle  ||
                             phase == GamePhase.DetermineOrder;
            bool showGame  = phase != GamePhase.Lobby &&
                             phase != GamePhase.Idle  &&
                             phase != GamePhase.GameEnd;
            bool showEnd   = phase == GamePhase.GameEnd;

            if (lobbyPanel != null) lobbyPanel.SetActive(showLobby);
            if (gamePanel  != null) gamePanel.SetActive(showGame);
            if (endPanel   != null) endPanel.SetActive(showEnd);

            if (phase == GamePhase.Lobby || phase == GamePhase.Idle)
                UpdateLobbyUI(ctrl);
            else if (phase == GamePhase.DetermineOrder)
                UpdateDetermineOrderUI(ctrl);
            else if (phase == GamePhase.GameEnd)
                UpdateEndUI(ctrl);
            else
                UpdateGameUI(ctrl);
        }

        private void UpdateLobbyUI(LudoGameController ctrl)
        {
            int  localSlot = ctrl.GetLocalPlayerSlot();
            bool isMaster  = Networking.IsMaster;

            for (int i = 0; i < 4; i++)
            {
                bool active = ctrl.IsPlayerActive(i);
                int  pid    = ctrl.GetPlayerId(i);

                if (playerNameTexts[i] != null)
                {
                    if (active)
                    {
                        VRCPlayerApi p = VRCPlayerApi.GetPlayerById(pid);
                        playerNameTexts[i].text = (p != null) ? p.displayName : "?";
                    }
                    else
                    {
                        playerNameTexts[i].text = "---";
                    }
                }

                if (joinButtons[i]  != null) joinButtons[i].SetActive(!active && localSlot == -1);
                if (leaveButtons[i] != null) leaveButtons[i].SetActive(active && i == localSlot);
            }

            if (startButton != null)
                startButton.SetActive(isMaster && ctrl.CountActivePlayers() >= 2);
        }

        private void UpdateDetermineOrderUI(LudoGameController ctrl)
        {
            for (int i = 0; i < 4; i++)
            {
                if (playerNameTexts[i] == null) continue;
                if (ctrl.IsPlayerActive(i))
                {
                    int r = ctrl.GetOrderRoll(i);
                    playerNameTexts[i].text = r > 0 ? r.ToString() : "?";
                }
                else
                {
                    playerNameTexts[i].text = "---";
                }
            }
            if (turnText != null) turnText.text = "Determining order...";
        }

        private void UpdateGameUI(LudoGameController ctrl)
        {
            int       localSlot = ctrl.GetLocalPlayerSlot();
            int       curSlot   = ctrl.GetCurrentSlot();
            GamePhase phase     = ctrl.GetPhase();

            if (turnText != null)
            {
                if (phase == GamePhase.SelectMove && curSlot == localSlot)
                    turnText.text = "Choose a token to move";
                else if (curSlot == localSlot)
                    turnText.text = "Your turn";
                else
                    turnText.text = "Player " + (curSlot + 1) + "'s turn";
            }

            if (diceText != null)
                diceText.text = (phase == GamePhase.TurnBegin)
                    ? "--"
                    : ctrl.GetDiceValue().ToString();

            bool canRoll = localSlot == curSlot && phase == GamePhase.TurnBegin;
            if (rollButton != null) rollButton.SetActive(canRoll);
        }

        private void UpdateEndUI(LudoGameController ctrl)
        {
            for (int i = 0; i < 4; i++)
            {
                if (rankTexts[i] == null) continue;
                if (ctrl.IsPlayerActive(i))
                {
                    int r = ctrl.GetPlayerRank(i);
                    rankTexts[i].text = r > 0 ? r + " place" : "---";
                }
                else
                {
                    rankTexts[i].text = "---";
                }
            }
        }

        // ─── Button Callbacks ────────────────────────────────────────────────
        public void OnRollClicked()
        {
            ConsoleLog("OnRollClicked");
            controller.OnRollPressed();
        }
        public void OnTokenClicked0()     => controller.OnTokenPressed(0);
        public void OnTokenClicked1()     => controller.OnTokenPressed(1);
        public void OnTokenClicked2()     => controller.OnTokenPressed(2);
        public void OnTokenClicked3()     => controller.OnTokenPressed(3);
        public void OnJoinSlot0Clicked()
        {
            ConsoleLog("OnJoinSlot0Clicked");
            controller.OnJoinSlotPressed(0);
        }
        public void OnJoinSlot1Clicked()
        {
            ConsoleLog("OnJoinSlot1Clicked");
            controller.OnJoinSlotPressed(1);
        }
        public void OnJoinSlot2Clicked()
        {
            ConsoleLog("OnJoinSlot2Clicked");
            controller.OnJoinSlotPressed(2);
        }
        public void OnJoinSlot3Clicked()
        {
            ConsoleLog("OnJoinSlot3Clicked");
            controller.OnJoinSlotPressed(3);
        }
        public void OnLeaveSlot0Clicked() => controller.OnLeaveSlotPressed(0);
        public void OnLeaveSlot1Clicked() => controller.OnLeaveSlotPressed(1);
        public void OnLeaveSlot2Clicked() => controller.OnLeaveSlotPressed(2);
        public void OnLeaveSlot3Clicked() => controller.OnLeaveSlotPressed(3);
        public void OnStartClicked()      => controller.OnStartGamePressed();
        public void OnResetClicked()      => controller.OnResetGamePressed();
    }
}
