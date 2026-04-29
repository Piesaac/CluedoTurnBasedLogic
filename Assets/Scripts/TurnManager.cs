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
        turnOrder.OnListChanged += (changeEvent) => updateUI();
        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();

        if (IsServer)
        {
            SetupTurnOrder();
            whosPlaying.Value = turnOrder[0];
        }
    
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
        if (whosPlaying.Value >= 100) return true;

        GameObject activeObj = findActiveplayer();
        if (activeObj != null && activeObj.TryGetComponent<Character>(out var character))
        {
            return character.isRobot.Value;
        }

        return false;
    }

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

    public void nextTurn()
    {
        if (!IsServer) return;
        int currentIndex = turnOrder.IndexOf(whosPlaying.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;
        whosPlaying.Value = turnOrder[nextIndex];
        whatPhase.Value = TurnStage.ROLLING;
    }

    public void pushNextPhase()
    {
        if (!IsServer) return; 
        nextPhaseServerRpc(); 
    }   

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

    [ClientRpc]
    private void updateClientUIClientRpc()
    {
        updateUI();
    }
}