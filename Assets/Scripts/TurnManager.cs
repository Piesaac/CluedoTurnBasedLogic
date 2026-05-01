using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using turnyWurny; 

public class TurnManager : NetworkBehaviour
{   // Set variables
    [Header("UI Text")]
    public TextMeshProUGUI status;
    public TextMeshProUGUI activePlayerText;

    [Header("Settings")]
    public NetworkVariable<TurnStage> whatPhase = new NetworkVariable<TurnStage>(TurnStage.ROLLING);
    public NetworkVariable<ulong> whosPlaying = new NetworkVariable<ulong>(0);

    public NetworkList<ulong> turnOrder = new NetworkList<ulong>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public List<ulong> originalPlayers = new List<ulong>();

    // Subscribes to event changes of network variables to automatically update UI.
    public override void OnNetworkSpawn()
    {
        turnOrder.OnListChanged += (changeEvent) => updateUI();
        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();

        // The Host initialises the turn order and sets the starting player index.
        if (IsServer)
        {
            SetupTurnOrder();
            whosPlaying.Value = turnOrder[0];
        }
    
        updateUI();
    }

    // Method to initialise turn order at start of game.
    private void SetupTurnOrder()
    {
        if (!IsServer) return;

        originalPlayers.Clear();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
        {
            originalPlayers.Add(client);
        }

        for (int i = 0; i < MenuController.numBotsToSpawn; i++)
        {
            originalPlayers.Add((ulong)(100 + i));
        }

        turnOrder.Clear();
        foreach (var id in originalPlayers)
        {
            turnOrder.Add(id);
        }
    }
    
    // Returns true if the player currently playing is an AI.
    public bool turingTest()
    {
        if (whosPlaying.Value >= 100) return true;

        GameObject activeObj = findActiveplayer();
        if (activeObj != null && activeObj.TryGetComponent<Character>(out var character))
        {
            return character.isRobot.Value;
        }

        return false;
    }

    // Returns the currently active player.
    private GameObject findActiveplayer()
    {
        ulong activeId = whosPlaying.Value;
        
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(activeId, out var client))
        {
            return client.PlayerObject.gameObject;
        }

        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.NetworkObjectId == activeId || (obj.IsOwner == false && obj.gameObject.name.Contains(activeId.ToString())))
            {
                return obj.gameObject;
            }
        }
        return null;
    }

    // Updates UI to show which player holds the turn and current phase of turn.
    private void updateUI()
    {
        if (activePlayerText != null)
        {
            ulong activeId = whosPlaying.Value;
            activePlayerText.text = "Current Player: " + getCharacter(activeId); 

            if (activeId == NetworkManager.Singleton.LocalClientId)
                activePlayerText.text += " (YOU)";
        }

        if (status != null)
        {
            status.text = "Current Phase: " + whatPhase.Value.ToString();
        }

    }

    // Returns the string name of the object ID inputted.
    private string getCharacter(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client))
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<Character>(out var character))
            {
                return character.charName;
            }
        }


        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (netObj.TryGetComponent<Character>(out var character))
            {
                if (character.isRobot.Value && (ulong)character.botID.Value == id)
                {
                    return character.charName;
                }
            }
        }
        return "Spectator";
    }

    // Allows clients to request next phase of turn if it is their turn, or always allows for server.
    public void reqNextPhase()
    {
        if (NetworkManager.Singleton.LocalClientId == whosPlaying.Value || IsServer) 
        {
            nextPhaseServerRpc();
        }
        else
        {
            Debug.Log("Not your turn");
        }
    }

    // Method to change the turn phase on the server.
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
    }

    // Method to push the game to the next turn.
    public void nextTurn()
    {
        int currentIndex = turnOrder.IndexOf(whosPlaying.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;
        whosPlaying.Value = turnOrder[nextIndex];
        whatPhase.Value = TurnStage.ROLLING;
    }

    // Method to push the game to the next turn phase.
    public void pushNextPhase()
    {
        if (!IsServer) return; 
        nextPhaseServerRpc(); 
    }   

    // Method to remove player for failed accusation.
    public void removePlayer(ulong id)
    {
        if (!IsServer) return;

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
            turnOrder.RemoveAt(indexToRemove);
            Debug.Log($"TurnManager: removePlayer() called | Removed ID {id} from Turn Order.");
        }

        if (whosPlaying.Value == id && turnOrder.Count > 0)
        {
            int nextIndex = indexToRemove % turnOrder.Count;
            whosPlaying.Value = turnOrder[nextIndex];
            whatPhase.Value = TurnStage.ROLLING;
        }
        
        updateUI();
    }
}