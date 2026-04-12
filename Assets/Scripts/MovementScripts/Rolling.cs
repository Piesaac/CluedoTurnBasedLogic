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
    public TextMeshProUGUI moves;

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
        if (turnMan.whosPlaying.Value != (int)NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Not your turn to roll!");
            return;
        }

        firstVal = UnityEngine.Random.Range(1, 7);
        secondVal = UnityEngine.Random.Range(1, 7);
        totalVal = firstVal + secondVal;
        
        moves.text = $"First dice rolled: {firstVal}; Second dice rolled: {secondVal}; Total score: {totalVal}";

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                Movement moveScript = localPlayer.GetComponent<Movement>();
                if (moveScript != null)
                {
                    moveScript.setMovesServerRpc(totalVal);
                }
            }
        }

        turnMan.reqNextPhase();

        firstVal = 0;
        secondVal = 0;
    }

    void Update()
    {
        if (turnMan == null) return;

        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING)
        {
            moves.text = " ";
        }
    }
}