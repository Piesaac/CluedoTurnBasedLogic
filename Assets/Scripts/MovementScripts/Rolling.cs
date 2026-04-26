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

        // Update the UI for everyone
        UpdateDiceUIClientRpc(firstVal, secondVal, totalVal);

        // Find the specific player object (Human or Bot)
        GameObject playerObj = GetPlayerObject(playerId);
        
        if (playerObj != null && playerObj.TryGetComponent<Movement>(out var moveScript))
        {
            moveScript.setMovesServerRpc(totalVal);
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
        // Search for human clients
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
            return client.PlayerObject.gameObject;

        // Search for bot objects in the scene
        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.TryGetComponent<Character>(out var character) && (ulong)character.botID.Value == id)
                return obj.gameObject;
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