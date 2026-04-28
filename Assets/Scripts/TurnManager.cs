using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using turnyWurny; // Ensure this namespace matches your TurnStage enum

public class TurnManager : NetworkBehaviour
{
    [Header("UI Text")]
    public TextMeshProUGUI status;
    public TextMeshProUGUI activePlayerText;

    [Header("Settings")]
    public NetworkVariable<TurnStage> whatPhase = new NetworkVariable<TurnStage>(TurnStage.ROLLING);
    public NetworkVariable<ulong> whosPlaying = new NetworkVariable<ulong>(0);

    public NetworkList<ulong> turnOrder = new NetworkList<ulong>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);



    public override void OnNetworkSpawn()
    {
        // Important: List events should be subscribed to by everyone
        turnOrder.OnListChanged += (changeEvent) => {
            Debug.Log("Client: Turn Order List Changed!");
            updateUI();
        };

        if (IsServer)
        {
            SetupTurnOrder();
            whosPlaying.Value = turnOrder[0];
        }

        // Subscribe to variable changes
        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();
    
        updateUI();
    }

    private void SetupTurnOrder()
    {
        if (!IsServer) return;

        List<ulong> initialOrder = new List<ulong>();
    
        foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
        {
            initialOrder.Add(client);
        }

        for (int i = 0; i < MenuController.numBotsToSpawn; i++)
        {
            initialOrder.Add((ulong)(100 + i));
        }

        turnOrder.Clear();
        foreach (var id in initialOrder)
        {
            turnOrder.Add(id);
        }
    }

    public bool turingTest()
    {
        Debug.Log($"TurnManager: turingTest() called");
        // Simple check: In your setup, IDs >= 100 are bots
        if (whosPlaying.Value >= 100) return true;

        // Fallback: Check the actual component if it exists
        GameObject activeObj = GetActivePlayerObject();
        if (activeObj != null && activeObj.TryGetComponent<Character>(out var character))
        {
            return character.isRobot.Value;
        }

        return false;
    }

    private GameObject GetActivePlayerObject()
    {
        ulong activeId = whosPlaying.Value;
        
        // Check humans
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(activeId, out var client))
        {
            return client.PlayerObject.gameObject;
        }

        // Check bots (searching spawned objects)
        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.NetworkObjectId == activeId || (obj.IsOwner == false && obj.gameObject.name.Contains(activeId.ToString())))
            {
                return obj.gameObject;
            }
        }
        return null;
    }

    private GameObject FindBotObject(ulong id)
    {
        return GetActivePlayerObject();
    }


    private void updateUI()
    {
        // 1. Update the Active Player Name
        if (activePlayerText != null)
        {
            ulong activeId = whosPlaying.Value;
            activePlayerText.text = "Current Player: " + getCharacter(activeId); 

            if (activeId == NetworkManager.Singleton.LocalClientId)
                activePlayerText.text += " (YOU)";
        }

        // 2. Update the Phase Text (This is what was missing)
        if (status != null)
        {
            // Converts the Enum (ROLLING, MOVING, etc.) to a string
            status.text = "Current Phase: " + whatPhase.Value.ToString();
        
            // Optional: Add a little color so it's obvious it changed
            status.color = Color.yellow; 
        }

        // Debug to console to verify values are actually reaching the client
        Debug.Log($"[UI DEBUG] Player: {whosPlaying.Value} | Phase: {whatPhase.Value}");
    }

    private string getCharacter(ulong id)
    {
        // 1. Try to find a human client first
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<Character>(out var character))
            {
                return character.charName;
            }
        }


        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.TryGetComponent<Character>(out var character))
            {

                if (character.isRobot.Value && id >= 100) 
                {
                    if (obj.gameObject.name.Contains(id.ToString())) return character.charName;
                }
            }
        }

        return "Spectator";
    }

    public void reqNextPhase()
    {
        if (NetworkManager.Singleton.LocalClientId == whosPlaying.Value || IsServer) 
        {
            nextPhaseServerRpc();
        }
        else
        {
            Debug.Log("Not your turn!");
        }
        Debug.Log($"TurnManager: reqNextPhase() called");
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
        else if (whatPhase.Value == TurnStage.SUGGESTING)
        {
            nextTurn();
        }
        Debug.Log($"TurnManager: nextPhaseServerRpc called | Phase is: {whatPhase.Value}");
    }

    // Inside TurnManager.cs
    public void nextTurn()
    {
        if (!IsServer) return;

        // 1. Get the next player in the rotation
        int currentIndex = turnOrder.IndexOf((ulong)whosPlaying.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;
        ulong nextPlayerId = turnOrder[nextIndex];

        // 2. Find their Character script
        GameObject nextPlayerObj = GetPlayerObjectById(nextPlayerId);
        if (nextPlayerObj != null && nextPlayerObj.TryGetComponent<Character>(out var character))
        {
            // 3. If they are out, skip them and call this function again
            if (character.isOut.Value)
            {
                whosPlaying.Value = nextPlayerId;
                nextTurn(); // Recursive call to find the next valid player
                return;
            }
        }

        // 4. Finally set the valid player and reset the phase
        whosPlaying.Value = nextPlayerId;
        whatPhase.Value = TurnStage.ROLLING; // This is what makes your Roll button reappear!
    }

    //use for removing the ais if they make a false accusation, rather than using other IDs since that caused bugs
    private GameObject GetPlayerObjectById(ulong id)
    {
        // 1. Check if it's a human player
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
        {
            if (client.PlayerObject != null) return client.PlayerObject.gameObject;
        }

        // 2. Check if it's a bot (searching all spawned objects)
        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.TryGetComponent<Character>(out var character))
            {
                // Match the ID to the bot's ID
                if ((ulong)character.botID.Value == id) return obj.gameObject;
            }
        }
        return null;
    }

    public void pushNextPhase()
    {
        Debug.Log($"TurnManager: pushNextPhase() called");
        // Safety check to ensure only the Server actually changes the NetworkVariable
        if (!IsServer) return; 
        // Calls the Rpc to move the TurnStage enum forward
        nextPhaseServerRpc(); 
    }   

    public void removePlayer(ulong id)
    {
        if (!IsServer) return;

        // Find the index manually to ensure we have it
        int indexToRemove = -1;
        for (int i = 0; i < turnOrder.Count; i++)
        {
            if (turnOrder[i] == id)
            {
                indexToRemove = i;
                break;
            }
        }

        if (indexToRemove != -1)
        {
            // Use RemoveAt - this triggers a specific 'Remove' event for Clients
            turnOrder.RemoveAt(indexToRemove);
            Debug.Log($"[Server] Removed ID {id} from Turn Order.");
        }

        // Logic for passing the turn if the current player was kicked
        if (whosPlaying.Value == id && turnOrder.Count > 0)
        {
            // Move to the next available person in the list
            int nextIndex = indexToRemove % turnOrder.Count;
            whosPlaying.Value = turnOrder[nextIndex];
            whatPhase.Value = TurnStage.ROLLING;
        }

        // Force an immediate UI refresh for the Host
        updateUI();
    }

    [ClientRpc]
    private void updateClientUIClientRpc()
    {
        updateUI();
    }
}