using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny; 

public class TurnManager : NetworkBehaviour
{
    [Header("UI Text")]
    public TextMeshProUGUI status;
    public TextMeshProUGUI activePlayerText;

    // Initialises the game to begin in the rolling phase
    public NetworkVariable<TurnStage> whatPhase = new NetworkVariable<TurnStage>(TurnStage.ROLLING);

    // Creates an index for the active player to iterate over the total players
    public NetworkVariable<int> whosPlaying = new NetworkVariable<int>(0);

    // Creates a count of total players using connected clients
    private int allPlayers => NetworkManager.Singleton.ConnectedClientsIds.Count;

    // When the phase or active player changes, updates the UI automatically.
    public override void OnNetworkSpawn()
    {
        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();
        updateUI();
    }


    // Method to update UI to show what phase and whos playing
    private void updateUI()
    {
        // Updates status (e.g. "ROLLING", "MOVING", "SUGGESTING")
        if (status != null)
        {
            status.text = whatPhase.Value.ToString() + "!";
        }

        // Updates text indicating whose turn it is
        if (activePlayerText != null)
        {
            if (whosPlaying.Value == 0)
            {
                activePlayerText.text = "Miss Scarlett: Player " + (whosPlaying.Value + 1);
            }
            else if (whosPlaying.Value == 1)
            {
                activePlayerText.text = "Colonel Mustard: Player " + (whosPlaying.Value + 1);
            }
            else
            {
                activePlayerText.text = "Player " + (whosPlaying.Value + 1);
            }
        
            // Indicates to the player if it is their turn specifically
            if (whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId)
            {
                activePlayerText.text += " (YOU)";
            }
        }
    }

    public void reqNextPhase()
    {
        // Only allows the player whose turn it is to press the button.
        if (NetworkManager.Singleton.LocalClientId != (ulong)whosPlaying.Value) 
        {
            Debug.Log("Not your turn!");
            return;
        }

        nextPhaseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void nextPhaseServerRpc()
    {
        // Logic happens ONLY on the Server/Host
        if (whatPhase.Value == TurnStage.ROLLING)
        {
            whatPhase.Value = TurnStage.MOVING;
        }
        else if (whatPhase.Value == TurnStage.MOVING)
        {
            whatPhase.Value = TurnStage.SUGGESTING;
        }
        else
        {
            // Once suggesting is finished, resets to rolling and moves to next player
            whatPhase.Value = TurnStage.ROLLING;
            nextTurn();
        }
    }

    public void nextTurn()
    {
        // Cycles to the next player in rotation
        if (allPlayers > 0)
        {
            whosPlaying.Value = (whosPlaying.Value + 1) % allPlayers;
        }
        whatPhase.Value = TurnStage.ROLLING;
    }

    public void pushNextPhase()
    {
        if (!IsServer) return; 
        nextPhaseServerRpc(); 
    }
}