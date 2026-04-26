using UnityEngine;
using TMPro;
using Unity.Netcode;
using turnyWurny;

public class Rolling : NetworkBehaviour
{
    public TurnManager turnMan;
    public TextMeshProUGUI diceResult;

    void Start()
    {
        if (turnMan == null) turnMan = Object.FindFirstObjectByType<TurnManager>();
    }

    // This is the new "Unified" roll method
    public void callRoll()
    {
        // 1. Validation Logic
        // Use the TurnManager's current ID instead of LocalClientId
        ulong activePlayerId = (ulong)turnMan.whosPlaying.Value;

        // If we are a client, only allow rolling if we own the active player
        if (!IsServer && activePlayerId != NetworkManager.Singleton.LocalClientId) return;
        
        // Ensure we are in the correct phase
        if (turnMan.whatPhase.Value != TurnStage.ROLLING) return;

        ExecuteRollServerRpc(activePlayerId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ExecuteRollServerRpc(ulong playerId)
    {
        // Roll logic happens on the Server for security
        int firstVal = Random.Range(1, 7);
        int secondVal = Random.Range(1, 7);
        int totalVal = firstVal + secondVal;
        
        // Find the specific player object (Human or Bot)
        GameObject playerObj = GetPlayerObject(playerId);
        
        if (playerObj != null && playerObj.TryGetComponent<Movement>(out var moveScript))
        {
            // On the server, we can modify NetworkVariables directly
            moveScript.move_tokens.Value = totalVal; 

            Debug.Log($"Server: Player {playerId} rolled {totalVal}. Tokens set.");

            // 3. Update the UI for everyone
            UpdateDiceUIClientRpc(firstVal, secondVal, totalVal);
        
            // 4. Advance phase
            turnMan.reqNextPhase(); 
        }
    }

    [ClientRpc]
    private void UpdateDiceUIClientRpc(int f, int s, int total)
    {
        if (diceResult != null)
            diceResult.text = $"Rolled: {f} + {s} = {total}";
    }

    private GameObject GetPlayerObject(ulong id)
    {
        // This is the most reliable way to find the object associated with a Client ID
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
        {
            return client.PlayerObject.gameObject;
        }
    
        // Fallback for Bots
        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (netObj.TryGetComponent<Character>(out var character) && (ulong)character.botID.Value == id)
            {
                return netObj.gameObject;
            }
        }
        return null;
    }

    void Update()
    {
        if (turnMan == null) return;
        if (turnMan.whatPhase.Value == TurnStage.SUGGESTING && diceResult != null)
        {
            diceResult.text = " ";
        }
    }
}