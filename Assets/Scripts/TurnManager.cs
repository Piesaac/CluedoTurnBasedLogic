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
    public NetworkVariable<int> whosPlaying = new NetworkVariable<int>(0);

    public List<ulong> turnOrder = new List<ulong>();

    private int allPlayers = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetupTurnOrder();
            // Start the game with the first ID in the list
            whosPlaying.Value = (int)turnOrder[0];
        }

        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();
        updateUI();
    }

    private void SetupTurnOrder()
    {
        turnOrder.Clear();

        // 1. Add all human Client IDs (typically 0, 1, 2...)
        foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
        {
            turnOrder.Add(client);
        }

        // 2. Add all Bot IDs (starting at 100 as per your PlayerSpawner)
        for (int i = 0; i < MenuController.numBotsToSpawn; i++)
        {
            turnOrder.Add((ulong)(100 + i));
        }
        allPlayers = turnOrder.Count;
        Debug.Log($"TurnManager: Sequence initialized with {allPlayers} players.");
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
        ulong activeId = (ulong)whosPlaying.Value;
        
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
        Debug.Log($"TurnManager: updateUI called");
        if (status != null)
        {
            status.text = whatPhase.Value.ToString() + "!";
        }

        if (activePlayerText != null)
        {
            int humanCount = NetworkManager.Singleton.ConnectedClients.Count;
            string playerName = "";

            if (whosPlaying.Value == 0) playerName = "Miss Scarlett";
            else if (whosPlaying.Value == 1) playerName = "Colonel Mustard";
            else playerName = "Player " + (whosPlaying.Value + 1);

            if (whosPlaying.Value >= humanCount)
            {
                activePlayerText.text = $"{playerName} (AI)";
            }
            else
            {
                activePlayerText.text = playerName;
                
                if (whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId)
                {
                    activePlayerText.text += " (YOU)";
                }
            }
        }
    }

    public void reqNextPhase()
    {
        if (NetworkManager.Singleton.LocalClientId == (ulong)whosPlaying.Value || IsServer) 
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

    public void nextTurn()
    {
        if (!IsServer) return;
        int currentIndex = turnOrder.IndexOf((ulong)whosPlaying.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;
        whosPlaying.Value = (int)turnOrder[nextIndex];
        whatPhase.Value = TurnStage.ROLLING;
        Debug.Log($"TurnManager: nextTurn() called | Turn passed to index: {whosPlaying.Value}");
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
        if (turnOrder.Contains(id))
        {
            turnOrder.Remove(id);
        }

        // If the person eliminated was the one currently playing, 
        // move to the next person immediately.
        if ((ulong)whosPlaying.Value == id)
        {
            nextTurn();
        }
    }
}