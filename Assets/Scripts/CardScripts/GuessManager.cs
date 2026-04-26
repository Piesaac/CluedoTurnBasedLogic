using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny; 
using CardList;
using System.Collections;
using System.Collections.Generic;

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
        Debug.Log("Button actually clicked");
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
        UIController.Instance.Invoke("hideDisproveText", 4f);
        
        // If Host, advance the phase because the sequence ended
        if(IsServer) Invoke("endDisproveServerRpc", 4.1f);
    }

    [ClientRpc]
    private void reqDisproveClientRpc(Card[] matchingCards, ClientRpcParams rpcParams)
    {
        uiscript.ShowDisprovePanel(matchingCards); 
    }

    public void validateAccuse()
    {
        chosenWho = uiscript.accuseWho;
        chosenWhat = uiscript.accuseWhat;
        chosenWhere = uiscript.accuseWhere;

        submitAccuseServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitAccuseServerRpc(Who who, What what, Where where)
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

        string message = (foundWho && foundWhat && foundWhere) 
            ? $"CORRECT! It was {who} with the {what} in the {where}!" 
            : "WRONG! Youuuu'rrrreee OUT!";

        returnAnswerClientRpc(message);
        turnMan.pushNextPhase();
    }

    [ClientRpc]
    private void returnAnswerClientRpc(string resultText)
    {
        guessResult.text = resultText;
        uiscript.clearGuessDropdowns();
        Invoke("clearAnswerText", 5f);
    }

    private void clearAnswerText()
    {
        guessResult.text = "";
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
}