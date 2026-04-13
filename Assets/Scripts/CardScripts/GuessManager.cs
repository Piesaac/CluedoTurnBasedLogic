using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny; 
using CardList;

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

    // Resets dropdowns to original form so next player does not see input.
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

        // 2. Ask the server to check them
        submitGuessServerRpc(chosenWho, chosenWhat, chosenWhere);
    }

    // Submits the guess to the server and checks them against the evidence selected.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void submitGuessServerRpc(Who who, What what, Where where)
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
