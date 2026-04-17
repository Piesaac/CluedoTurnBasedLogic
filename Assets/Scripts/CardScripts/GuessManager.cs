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



    // Initialises variables as selected items in dropdown
    void Start()
    {
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;

    }

    // Resets dropdowns to original form so next player does not see previous players input
    private void resetGuess()
    {
        uiscript.clearGuessDropdowns();
    }

    // Re-initialises the local variables with dropdown input - links with the submit button.
    public void validateGuess()
    {
        chosenWho = uiscript.selectedSuspect;
        chosenWhat = uiscript.selectedWeapon;
        chosenWhere = uiscript.selectedRoom;

        submitGuessServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    // Submits the guess to the server and checks them against the evidence selected.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitGuessServerRpc(Who who, What what, Where where, RpcParams rpcParams = default)
    {   
        // Finds ulong ID of player guessing.
        ulong guesserId = rpcParams.Receive.SenderClientId;

        // Finds next clockwise player.
        int nextCW_Player = (int)(guesserId + 1) % cardDist.playerHands.Count;
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
        for (int i = 0; i < cardDist.playerHands.Count - 1; i++)
        {
            int idxToCheck = (start + i) % cardDist.playerHands.Count;
            if (idxToCheck == (int)guesserID) continue;

            List<Card> foundCards = findSame(cardDist.playerHands[idxToCheck], who, what, where);
            
            if (foundCards.Count > 0)
            {
                ulong clientToNotify = NetworkManager.Singleton.ConnectedClientsIds[(int)idxToCheck];
                ClientRpcParams param = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams {TargetClientIds = new ulong[] {clientToNotify}}
                };
                reqDisproveClientRpc(foundCards.ToArray(), param);
                yield break;
            }

        }
        notifyNoMatchesClientRpc(guesserID);
    }

    [ClientRpc]
    private void notifyNoMatchesClientRpc(ulong playerID)
    {

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
            if (evidenceCard.type == Card.CardType.Suspect && evidenceCard.value == (int)who)
                foundWho = true;
            
            if (evidenceCard.type == Card.CardType.Weapon && evidenceCard.value == (int)what)
                foundWhat = true;
            
            if (evidenceCard.type == Card.CardType.Room && evidenceCard.value == (int)where)
                foundWhere = true;
        }

        string message = (foundWho && foundWhat && foundWhere) 
            ? $"CORRECT! It was {who} with the {what} in the {where}!" 
            : "WRONG! Youuuu'rrrreee OUT!";

            returnAnswerClientRpc(message);
            turnMan.pushNextPhase();
    }

    // Updates client UI with confirmation of their input
    [ClientRpc]
    private void returnAnswerClientRpc(string resultText)
    {
        guessResult.text = resultText;

        uiscript.clearGuessDropdowns();
    
        Invoke("clearAnswerText", 5f);
    }

    // Clears the confirmation text for the next player
    private void clearAnswerText()
    {
        guessResult.text = "";
    }   

}