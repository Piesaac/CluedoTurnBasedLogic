using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny; 
using CardList;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GuessManager : NetworkBehaviour
{
    [Header("Links to UI controller")]
    [SerializeField] private UIController uiscript;

    [Header("Links to Card System")]
    [SerializeField] private CardDistributor cardDist;

    [Header("Links to Turn Manager")]
    [SerializeField] private TurnManager turnMan;

    [Header("Fields for guessing")]
    public Who chosenWho;
    public What chosenWhat;
    public Where chosenWhere;
    [SerializeField] public TextMeshProUGUI guessResult;

    [SerializeField] public GameObject gameplayPanel;
    [SerializeField] public GameObject spectatorPanel;
    [SerializeField] public TextMeshProUGUI spectatorText;

    [SerializeField] private WSPoint[] spawnPoints;

    public static GuessManager Instance;

    void Start()
    {
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void resetGuess()
    {
        uiscript.clearGuessDropdowns();
    }

    public void validateGuess()
    {
        uiscript.suggestionButton();
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;

        submitGuessServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitGuessServerRpc(Who who, What what, Where where, RpcParams rpcParams = default)
    {   
        Debug.Log("GuessManager: submitGuessServerRpc() called");
        ulong guesserId = rpcParams.Receive.SenderClientId;

        // Use playerHands.Count (total players) instead of just human clients
        int nextCW_Player = ((int)guesserId + 1) % cardDist.playerHands.Count;
        StartCoroutine(checkTheirMFHands(who, what, where, nextCW_Player, guesserId));
    }

    private List<Card> findSame(List<Card> hand, Who who, What what, Where where)
    {
        List<Card> matches = new List<Card>();
        foreach (Card card in hand)
        {
            if ((card.type == Card.CardType.Suspect && card.value == (int)who) ||
                (card.type == Card.CardType.Weapon && card.value == (int)what) ||
                (card.type == Card.CardType.Room && card.value == (int)where))
            {
                matches.Add(card);
            }
        }
        return matches;
    }

    private IEnumerator checkTheirMFHands(Who who, What what, Where where, int start, ulong guesserID)
    {
        int totalPlayers = cardDist.playerHands.Count;
        int humanCount = NetworkManager.Singleton.ConnectedClients.Count;

        for (int i = 0; i < totalPlayers; i++)
        {
            int idxToCheck = (start + i) % totalPlayers;
            if (idxToCheck == (int)guesserID) continue;

            List<Card> foundCards = findSame(cardDist.playerHands[idxToCheck], who, what, where);
            
            if (foundCards.Count > 0)
            {
                // CHECK IF AI OR HUMAN
                if (idxToCheck < humanCount)
                {
                    // HUMAN: Send RPC to their specific ClientID
                    ulong clientToNotify = NetworkManager.Singleton.ConnectedClientsIds[idxToCheck];
                    ClientRpcParams param = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientToNotify } }
                    };
                    reqDisproveClientRpc(foundCards.ToArray(), param);
                }
                else
                {
                    // AI: Automatically pick the first card and disprove
                    string aiCardName = cardDist.whatCard(foundCards[0]);
                    Debug.Log($"AI Player {idxToCheck} disproving with {aiCardName}");
                    disproveServerRpc(aiCardName); 
                }
                yield break;
            }
        }
        notifyNoMatchesClientRpc(guesserID);
    }

    [ClientRpc]
    private void notifyNoMatchesClientRpc(ulong playerID)
    {
        // Only show this to the player who guessed
        if (NetworkManager.Singleton.LocalClientId != playerID) return;

        UIController.Instance.disproveText.text = "No cards found!";
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.Invoke("hideDisproveText", 3f);
        
        // If Host, advance the phase because the sequence ended
        if(IsServer) Invoke("endDisproveServerRpc", 3f);
    }

    [ClientRpc]
    private void reqDisproveClientRpc(Card[] matchingCards, ClientRpcParams rpcParams)
    {
        uiscript.ShowDisprovePanel(matchingCards); 
    }

    public void validateAccuse()
    {
        uiscript.confirmAccuse();
        chosenWho = uiscript.accuseWho;
        chosenWhat = uiscript.accuseWhat;
        chosenWhere = uiscript.accuseWhere;

        submitAccuseServerRpc(chosenWho, chosenWhat, chosenWhere, NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitAccuseServerRpc(Who who, What what, Where where, ulong requesterNetId, RpcParams rpcParams = default)
    {
        bool foundWho = false;
        bool foundWhat = false;
        bool foundWhere = false;

        foreach (Card evidenceCard in cardDist.evidence)
        {
            if (evidenceCard.type == Card.CardType.Suspect && evidenceCard.value == (int)who) foundWho = true;
            if (evidenceCard.type == Card.CardType.Weapon && evidenceCard.value == (int)what) foundWhat = true;
            if (evidenceCard.type == Card.CardType.Room && evidenceCard.value == (int)where) foundWhere = true;
        }
        bool isWinner = foundWho && foundWhat && foundWhere;

        if (isWinner)
        {
            ulong winnerId = rpcParams.Receive.SenderClientId;
            string winnerName = "N/A";
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(winnerId, out var client))
            {
                if (client.PlayerObject.TryGetComponent<Character>(out var character))
                {
                    winnerName = character.charName;
                }
            }
            endGameClientRpc(winnerId, winnerName);    
        }
        else
        {
            kickTheLoser(requesterNetId, rpcParams.Receive.SenderClientId);
        }
    }


    private void kickTheLoser(ulong playerId, ulong networkId)
    {
        if (turnMan != null)
        {
            turnMan.removePlayer(playerId);
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            if (client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<Movement>(out var moveScript))
                {
                    Tile currentTile = moveScript.stage.GetComponent<Tile>();
                    currentTile.updateOccupied(false);
                    moveScript.SetPlayerVisibilityClientRpc(false); 
                }
                if (client.PlayerObject.TryGetComponent<Character>(out var charScript))
                {
                    charScript.isOut.Value = true;
                }
            }
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } }
        };
        tellEmTheyLostClientRpc(clientRpcParams);
        checkForLoneSurvivor();
    }



    [ClientRpc]
    private void tellEmTheyLostClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        gameplayPanel.SetActive(false);
        spectatorText.text = "Accusation Wrong! You are now spectating.";
        spectatorPanel.SetActive(true);
        Invoke("hideSpectatorText", 4f);
    }

    private void hideSpectatorText()
    {
        spectatorPanel.SetActive(false);
    }





    [ClientRpc]
    private void endGameClientRpc(ulong id, string name)
    {
        // Save data to our static class on EVERY client
        AccuseResult.winID = id;
        AccuseResult.winName = name;
        AccuseResult.gameEnd = true;


        if (IsServer)
        {
            NetworkManager.SceneManager.LoadScene("End", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }



    public void disproveResult(string cardName)
    {
        disproveServerRpc(cardName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void disproveServerRpc(string cardName)
    {
        // Suggester is always the current player in turn manager
        ulong suggesterId = (ulong)turnMan.whosPlaying.Value;

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { suggesterId } }
        };

        notifDispResClientRpc(cardName, clientRpcParams);
        
        // Since a card was shown, the phase needs to end automatically after a delay
        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            turnMan.pushNextPhase();
        }
        else 
        {
            Debug.LogWarning("GuessManager tried to push phase, but we are no longer suggesting!");
        }
    }

    [ClientRpc]
    private void notifDispResClientRpc(string cardName, ClientRpcParams clientRpcParams = default)
    {
        UIController.Instance.disproveText.text = "You have been shown the card: " + cardName;
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.Invoke("hideDisproveText", 4f);
    }

    public void endDisprove()
    {
        endDisproveServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void endDisproveServerRpc()
    {
        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            turnMan.pushNextPhase();
        }
    }

    public void RequestMoveWeapon(string weaponName, string roomName)
    {
        MoveWeaponServerRpc(weaponName, roomName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void MoveWeaponServerRpc(string weaponName, string roomName)
    {
        Weapon weapon = FindObjectsByType<Weapon>(FindObjectsSortMode.None)
            .FirstOrDefault(w => w.myName == weaponName);

        if (weapon == null)
        {
            Debug.LogWarning($"Weapon not found: {weaponName}");
            return;
        }

        // Find matching room spawn point
        WSPoint targetPoint = spawnPoints
            .FirstOrDefault(p => p.Name == roomName);

        if (targetPoint == null)
        {
            Debug.LogWarning($"Room not found: {roomName}");
            return;
        }

        // Move weapon
        weapon.transform.position = targetPoint.transform.position;
        weapon.transform.rotation = targetPoint.transform.rotation;

        // Ensure it's networked
        NetworkObject netObj = weapon.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
        }
    }

    public void testTP()
    {
        RequestMoveWeapon("Candle_stick", "Kitchen");
    }

    private void checkForLoneSurvivor()
    {
        if (!IsServer) return;

        // 1. Find all Character scripts in the scene
        Character[] allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        List<Character> activePlayers = new List<Character>();

        foreach (Character c in allCharacters)
        {
            // Only count players who haven't been kicked
            if (!c.isOut.Value)
            {
                activePlayers.Add(c);
            }
        }

        // 2. If only 1 player remains, they win!
        if (activePlayers.Count == 1)
        {
            Character winner = activePlayers[0];
            Debug.Log($"<color=green>WIN BY DEFAULT: {winner.charName} is the last survivor!</color>");

            // Use the Host's ID or the Bot's ID depending on who it is
            ulong winnerId = winner.isRobot.Value ? (ulong)winner.botID.Value : winner.OwnerClientId;

            endGameClientRpc(winnerId, winner.charName);
        }
    }

}
