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

    // Button to validate guess made in suggestion phase, refreshes values from TMP_Dropdowns through uiscript and submits guess to the server.
    public void validateGuess()
    {
        uiscript.suggestionButton();
        uiscript.ToggleSuggestionLists(false);
        uiscript.confirmAccuseButton.gameObject.SetActive(false);
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;

        submitGuessServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    // Receives the request to submit the guess and checks the values guessed against player hands using checkTheirMFHands().
    // Triggers the movement of Character models and Weapon models to the room the suggestion was made in.
   [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitGuessServerRpc(Who who, What what, Where where, RpcParams rpcParams = default)
    {   
        ulong guesserId = rpcParams.Receive.SenderClientId;
        int guesserIdx = turnMan.originalPlayers.IndexOf(guesserId);
    
        StartCoroutine(checkTheirMFHands(who, what, where, guesserIdx, guesserId));
        activateGuessMoves(who, what, where);
    }

    // The method to execute the character and weapon movement to rooms after suggestion.
    private void activateGuessMoves(Who who, What what, Where where)
    {
        Debug.Log($"{who.ToString()} {what.ToString()} {where.ToString()} ");
        MoveCharacter(who.ToString(), where.ToString());
        MoveWeapon(what.ToString(), where.ToString());
    }

    // ---------- GUESSING - TELEPORTATION LOGIC ---------

    // The method that actually actions the movement of the weapon prefab.
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

    // The method used to move the Character model to the room in question.
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

    // Finds if any of the clues suggested match any in hand.
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


    private IEnumerator checkTheirMFHands(Who who, What what, Where where, int startIdx, ulong guesserID)
    {
        int total = turnMan.originalPlayers.Count;
        Debug.Log($"[DEBUG] Starting check. Total Players: {total}. Start Index: {startIdx}");

        for (int i = 1; i < total; i++)
        {
            int currentPointer = (startIdx + i) % total;
        
            // Ensure the pointer is valid before accessing the list
            if (currentPointer < 0 || currentPointer >= turnMan.originalPlayers.Count) {
                Debug.LogError($"[ERROR] currentPointer {currentPointer} is out of bounds!");
                yield break;
            }

            ulong targetId = turnMan.originalPlayers[currentPointer];
            Debug.Log($"[DEBUG] Checking Player {targetId} at index {currentPointer}");

            // VITAL: Ensure the hand exists for this index
            if (cardDist.playerHands[currentPointer] == null) {
                Debug.LogError($"[ERROR] playerHands[{currentPointer}] is NULL!");
                continue; 
            }

            List<Card> foundCards = findSame(cardDist.playerHands[currentPointer], who, what, where);
        
            if (foundCards.Count > 0)
            {
                if (targetId >= 100) // AI Logic
                {
                    string aiCardName = cardDist.whatCard(foundCards[0]);
                    disproveServerRpc(aiCardName);
                }
                else // Human Logic
                {
                    ClientRpcParams param = new ClientRpcParams {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetId } }
                    };
                    reqDisproveClientRpc(foundCards.ToArray(), param);
                    Debug.Log($"[SUCCESS] Sent Disprove Request to Client {targetId}");
                }
                yield break; 
            }
        }

        // If we get here, no matches were found
        Debug.Log("[DEBUG] No matches found. Notifying Suggester.");
        notifyNoMatchesClientRpc(guesserID);
    }

    // Method that notifies the suggester that no matching cards were found.
    [ClientRpc]
    private void notifyNoMatchesClientRpc(ulong playerID)
    {
        if (NetworkManager.Singleton.LocalClientId != playerID) return;

        UIController.Instance.disproveText.text = "No cards found!" + " | Skip or Accuse";
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.fullyfillGuesses();
        UIController.Instance.startAccuse();
    }


    // Method that prompts client with matching card to guess to disprove.
    [ClientRpc]
    private void reqDisproveClientRpc(Card[] matchingCards, ClientRpcParams rpcParams)
    {
        Debug.Log("CLIENT RECEIVED RPC");
        uiscript.isDisprove = true;
        uiscript.ShowDisprovePanel(matchingCards); 
        uiscript.UpdateUIVisibility();
    }

    // Called by UI Controller to receive the clue to disprove from the client.
    public void disproveResult(string cardName)
    {
        disproveServerRpc(cardName);
    }

    // Uses the clue received from the disprover and notifies the suggester (current active player)
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

    // Method used to notify the suggesting player, and allow them the opportunity to Accuse or skip.
    [ClientRpc]
    private void notifDispResClientRpc(string cardName, ClientRpcParams clientRpcParams = default)
    {
        UIController.Instance.disproveText.text = "You have been shown the card: " + cardName + " | Skip or Accuse";
        UIController.Instance.disproveText.gameObject.SetActive(true);
        UIController.Instance.fullyfillGuesses();
        UIController.Instance.startAccuse();
    }

    // ------- ACCUSATION LOGIC ----------

    // Used by the accuse button to check the contents of the dropdowns and submit them to be checked by the server.
    public void validateAccuse()
    {
        uiscript.confirmAccuse();
        chosenWho = uiscript.accuseWho;
        chosenWhat = uiscript.accuseWhat;
        chosenWhere = uiscript.accuseWhere;

        submitAccuseServerRpc(chosenWho, chosenWhat, chosenWhere, NetworkObjectId);
    }

    // Checks the accusations made by the player against the evidence cards made by Card Distributor.
    // If the accusation is correct, activates endGameClientRpc(), otherwise it removes the accusing player.
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

    // Used to remove the player from TurnManager logic and to activate methods within Movement to make their player prefab uninteractable.
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
        // Creates a string with the actual evidence clues to be shown to the player accusing.
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

    // If player has lost, tells them they are now a spectator and shows them the correct evidence.
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

        Invoke("hideSpectatorText", 3f); 
    }

    // Used by the method above to hide the notification of removal after a few seconds.
    private void hideSpectatorText()
    {
        UIController.Instance.disproveText.gameObject.SetActive(false);
        spectatorPanel.SetActive(false);
    }


    // ----------- GAME END LOGIC -----------

    // If the accusation is correct, this triggers and loads the game end scene.
    [ClientRpc]
    private void endGameClientRpc(ulong id, string name)
    {
        AccuseResult.winID = id;
        AccuseResult.winName = name;


        if (IsServer)
        {
            NetworkManager.SceneManager.LoadScene("End", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }


    // Checks if only one player remains by accessing isOut within the players Character script, if only one player remains, they are marked as the winner.
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
