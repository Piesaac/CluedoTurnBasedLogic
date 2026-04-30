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

    // ----------- GUESSING LOGIC --------------

    public void validateGuess()
    {
        uiscript.suggestionButton();
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;

        submitGuessServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    private void resetGuess()
    {
        uiscript.clearGuessDropdowns();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitGuessServerRpc(Who who, What what, Where where, RpcParams rpcParams = default)
    {   
        Debug.Log($"GuessManager: submitGuessServerRpc() called {who.ToString()} {what.ToString()} {where.ToString()}");
        ulong guesserId = rpcParams.Receive.SenderClientId;
        int nextCW_Player = ((int)guesserId + 1) % cardDist.playerHands.Count;
        StartCoroutine(checkTheirMFHands(who, what, where, nextCW_Player, guesserId));
        activateGuessMoves(who, what, where);
    }

    private void activateGuessMoves(Who who, What what, Where where)
    {
        Debug.Log($"{who.ToString()} {what.ToString()} {where.ToString()} ");
        MoveCharacter(who.ToString(), where.ToString());
        MoveWeapon(what.ToString(), where.ToString());
    }

    // ---------- GUESSING - TELEPORTATION LOGIC ---------

    public void RequestMoveWeapon(string weaponName, string roomName)
    {
        MoveWeapon(weaponName, roomName);
    }


    private void MoveWeapon(string weaponName, string roomName)
    {
        Weapon weapon = FindObjectsByType<Weapon>(FindObjectsSortMode.None)
            .FirstOrDefault(w => w.myName == weaponName);

        if (weapon == null)
        {
            Debug.LogWarning($"Weapon not found: {weaponName}");
            return;
        }

        WSPoint targetPoint = spawnPoints
            .FirstOrDefault(p => p.Name == roomName);

        if (targetPoint == null)
        {
            Debug.LogWarning($"Room not found: {roomName}");
            return;
        }
        weapon.transform.position = targetPoint.transform.position;
        weapon.transform.rotation = targetPoint.transform.rotation;
        NetworkObject netObj = weapon.GetComponent<NetworkObject>();
        Debug.Log($"Weapon: {weaponName} has been moved to room: {roomName}");
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
        }
    }

 
    private void MoveCharacter(string susName, string roomName)
    {
        Debug.Log("MoveCharacter() called");
        Character targetChar = FindObjectsByType<Character>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.charName == susName);

        if (targetChar == null)
        {
            Debug.Log("Character not found");

        }
        Debug.Log($"Room name: {roomName}");

        Door targetDoor = FindObjectsByType<Door>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.roomName == roomName);

        if (targetDoor != null)
        {
            Debug.Log("Target door found");

            int idToMatch = targetChar.isRobot.Value ? targetChar.botID.Value  : (int)targetChar.OwnerClientId;

            Vector3 spawnPos = targetDoor.GetRoomPosition(idToMatch);

            if (spawnPos != Vector3.zero)
            {
                Movement moveScript = targetChar.GetComponentInParent<Movement>() ?? targetChar.GetComponentInChildren<Movement>();
                if (moveScript != null)
                {
                    moveScript.TeleportToRoomServerRpc(spawnPos, roomName);
                    Debug.Log($"Player: {susName} has been moved to room: {roomName}");
                }
            }
        }
    }

    // ---------- DISPROVE LOGIC -----------

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
                if (idxToCheck < humanCount)
                {
                    ulong clientToNotify = NetworkManager.Singleton.ConnectedClientsIds[idxToCheck];
                    ClientRpcParams param = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientToNotify } }
                    };
                    reqDisproveClientRpc(foundCards.ToArray(), param);
                }
                else
                {
                    string aiCardName = cardDist.whatCard(foundCards[0]);
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
        if (NetworkManager.Singleton.LocalClientId != playerID) return;

        UIController.Instance.disproveText.text = "No cards found!" + " | Skip or Accuse";
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.fullyfillGuesses();
    }

    [ClientRpc]
    private void reqDisproveClientRpc(Card[] matchingCards, ClientRpcParams rpcParams)
    {
        uiscript.ShowDisprovePanel(matchingCards); 
    }

    public void disproveResult(string cardName)
    {
        disproveServerRpc(cardName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void disproveServerRpc(string cardName)
    {
        ulong suggesterId = (ulong)turnMan.whosPlaying.Value;

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { suggesterId } }
        };

        notifDispResClientRpc(cardName, clientRpcParams);

    }

    [ClientRpc]
    private void notifDispResClientRpc(string cardName, ClientRpcParams clientRpcParams = default)
    {
        UIController.Instance.disproveText.text = "You have been shown the card: " + cardName + " | Skip or Accuse";
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.fullyfillGuesses();
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

    // ------- ACCUSATION LOGIC ----------

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
            kickTheLoser(rpcParams.Receive.SenderClientId);
        }
    }


    // ------- ACCUSATION LOGIC - LOSER LOGIC -------

    private void kickTheLoser(ulong playerId)
    {
        Debug.Log("kicktheLoser() called");
        if (turnMan != null)
        {
            Debug.Log("kicktheLoser(): turnman not null");
            turnMan.removePlayer(playerId);
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            if (client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<Movement>(out var moveScript))
                {
                    moveScript.SetPlayerVisibilityClientRpc(false); 
                    Debug.Log("kicktheLoser(): movement script found and player made invisible");
                }
                if (client.PlayerObject.TryGetComponent<Character>(out var charScript))
                {
                    charScript.isOut.Value = true;
                    Debug.Log("kicktheLoser(): player marked as out");
                }
            }
        }
        string evidenceMessage = "";
        if (cardDist.evidence != null && cardDist.evidence.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (var card in cardDist.evidence)
            {
                names.Add(cardDist.whatCard(card));
            }
            evidenceMessage = string.Join(", ", names);
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } }
        };

        tellEmTheyLostClientRpc(evidenceMessage, clientRpcParams);
        checkForLoneSurvivor();
    }

    [ClientRpc]
    private void tellEmTheyLostClientRpc(string evidenceNames, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        gameplayPanel.SetActive(false);
    
        string resultMessage = "Accusation Wrong! You are now spectating.\n";
        resultMessage += "The correct clues were:\n";
    
        resultMessage += string.Join(", ", evidenceNames);

        spectatorText.text = resultMessage;
        spectatorPanel.SetActive(true);

        Invoke("hideSpectatorText", 6f); 
    }

    private void hideSpectatorText()
    {
        spectatorPanel.SetActive(false);
    }


    // ----------- GAME END LOGIC -----------


    [ClientRpc]
    private void endGameClientRpc(ulong id, string name)
    {
        AccuseResult.winID = id;
        AccuseResult.winName = name;
        AccuseResult.gameEnd = true;


        if (IsServer)
        {
            NetworkManager.SceneManager.LoadScene("End", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }



    private void checkForLoneSurvivor()
    {
        if (!IsServer) return;

        Character[] allCharacters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        List<Character> activePlayers = new List<Character>();

        foreach (Character c in allCharacters)
        {
            if (!c.isOut.Value)
            {
                activePlayers.Add(c);
            }
        }

        if (activePlayers.Count == 1)
        {
            Character winner = activePlayers[0];
            Debug.Log($"<color=green>WIN BY DEFAULT: {winner.charName} is the last survivor!</color>");

            ulong winnerId = winner.isRobot.Value ? (ulong)winner.botID.Value : winner.OwnerClientId;

            endGameClientRpc(winnerId, winner.charName);
        }
    }

}
