using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny;

public class Rolling : NetworkBehaviour
{
    private int firstVal;
    private int secondVal;
    private int totalVal;
    public TurnManager turnMan;
    public TextMeshProUGUI diceResult;

    // Initialises dice values as 0 and finds TurnManager. 
    void Start()
    {
        firstVal = 0;
        secondVal = 0;
        if (turnMan == null) turnMan = Object.FindFirstObjectByType<TurnManager>();
    }

    // Method used to roll the dice.
    public void callRoll()
    {
        Debug.Log("Rolling: callRoll() called");
        // 1. Validation
        if (turnMan.whosPlaying.Value != (int)NetworkManager.Singleton.LocalClientId) return;
        if (turnMan.whatPhase.Value != TurnStage.ROLLING) return; // Prevent double-rolling

        // 2. Roll Logic
        firstVal = UnityEngine.Random.Range(1, 7);
        secondVal = UnityEngine.Random.Range(1, 7);
        totalVal = firstVal + secondVal;
    
        diceResult.text = $"Rolled: {firstVal} + {secondVal} = {totalVal}";

        // 3. Communicate to Server
        if (NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent<Movement>(out var moveScript))
        {
            // Tell the server the value
            moveScript.setMovesServerRpc(totalVal);
        
            // IMPORTANT: The TurnManager should handle the phase shift 
            // ONLY after the moves are successfully set.
            turnMan.reqNextPhase(); 
        }

        // Reset local values
        firstVal = 0;
        secondVal = 0;
    }

    void Update()
    {
        if (turnMan == null) return;

        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            diceResult.text = " ";
        }
    }
}