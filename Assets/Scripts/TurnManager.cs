using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using turnyWurny; // Ensure this namespace matches your TurnStage enum

public class TurnManager : NetworkBehaviour
{
    [Header("UI Text")]
    public TextMeshProUGUI status;
    public TextMeshProUGUI activePlayerText;

    [Header("Settings")]
    public NetworkVariable<TurnStage> whatPhase = new NetworkVariable<TurnStage>(TurnStage.ROLLING);
    public NetworkVariable<int> whosPlaying = new NetworkVariable<int>(0);

    private int allPlayers = 0;
    private bool isAIBusy = false;

    public override void OnNetworkSpawn()
    {
        // 1. Calculate total player pool (Humans from NetworkManager + AI from Menu settings)
        if (IsServer)
        {
            allPlayers = NetworkManager.Singleton.ConnectedClients.Count + MenuController.numBotsToSpawn;
            Debug.Log($"TurnManager: Total players in rotation: {allPlayers}");
        }

        // 2. Subscribe to changes to keep UI in sync across all clients
        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();
        
        updateUI();
    }

    private void Update()
    {
        // Only the Server/Host should run AI logic
        if (!IsServer) return;

        // 3. Determine if it's currently an AI's turn
        // Humans are indices 0 to (HumanCount - 1). Bots start after that.
        int humanCount = NetworkManager.Singleton.ConnectedClients.Count;

        if (whosPlaying.Value >= humanCount && !isAIBusy)
        {
            StartCoroutine(roboTurn());
        }
    }

    private IEnumerator roboTurn()
    {
        isAIBusy = true;
        Debug.Log($"AI Player {whosPlaying.Value + 1} is thinking...");

        // Pause for realism so humans can read the UI
        yield return new WaitForSeconds(2f);

        // AI Cycles through all 3 phases automatically
        while (whosPlaying.Value >= NetworkManager.Singleton.ConnectedClients.Count)
        {
            nextPhaseServerRpc();
            
            // Wait for the next phase to be processed
            yield return new WaitForSeconds(2f);

            // If the phase reset to ROLLING, it means the turn ended
            if (whatPhase.Value == TurnStage.ROLLING)
                break;
        }

        isAIBusy = false;
    }

    private void updateUI()
    {
        if (status != null)
        {
            status.text = whatPhase.Value.ToString() + "!";
        }

        if (activePlayerText != null)
        {
            int humanCount = NetworkManager.Singleton.ConnectedClients.Count;
            string playerName = "";

            // Custom Names for the first two slots
            if (whosPlaying.Value == 0) playerName = "Miss Scarlett";
            else if (whosPlaying.Value == 1) playerName = "Colonel Mustard";
            else playerName = "Player " + (whosPlaying.Value + 1);

            // Label as AI if applicable
            if (whosPlaying.Value >= humanCount)
            {
                activePlayerText.text = $"{playerName} (AI)";
            }
            else
            {
                activePlayerText.text = playerName;
                
                // Show (YOU) only to the specific local player
                if (whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId)
                {
                    activePlayerText.text += " (YOU)";
                }
            }
        }
    }

    public void reqNextPhase()
    {
        // Allow the button to work if:
        // A) It is the local player's turn
        // B) It is an AI turn but the Host wants to force it forward
        if (NetworkManager.Singleton.LocalClientId == (ulong)whosPlaying.Value || IsServer) 
        {
            nextPhaseServerRpc();
        }
        else
        {
            Debug.Log("Not your turn!");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void nextPhaseServerRpc()
    {
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
            nextTurn();
        }
    }

    public void nextTurn()
    {
        if (!IsServer) return;

        // Move to next player index and wrap around using the total (Humans + AI)
        if (allPlayers > 0)
        {
            whosPlaying.Value = (whosPlaying.Value + 1) % allPlayers;
        }
        
        whatPhase.Value = TurnStage.ROLLING;
        Debug.Log($"Turn passed to index: {whosPlaying.Value}");
    }

    public void pushNextPhase()
    {
        // Safety check to ensure only the Server actually changes the NetworkVariable
        if (!IsServer) return; 
        // Calls the Rpc to move the TurnStage enum forward
        nextPhaseServerRpc(); 
    }   
}