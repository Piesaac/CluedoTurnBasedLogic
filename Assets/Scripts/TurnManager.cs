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
    private bool isAIBusy = false;

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

        Debug.Log($"TurnManager: Sequence initialized with {turnOrder.Count} players.");
    }

    private void Update()
    {
        if (!IsServer) return;

        // Check if the current ID is one of our Bots (>= 100)
        if (whosPlaying.Value >= 100 && !isAIBusy)
        {
            StartCoroutine(roboTurn());
        }
    }

    public bool turingTest()
    {
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

    private IEnumerator roboTurn()
    {
        isAIBusy = true;
        Debug.Log($"AI Player {whosPlaying.Value + 1} is thinking...");

        // Pause for realism so humans can read the UI
        yield return new WaitForSeconds(2f);

        GameObject activePlayerObj = NetworkManager.Singleton.ConnectedClientsIds.Contains((ulong)whosPlaying.Value) 
        ? NetworkManager.Singleton.ConnectedClients[(ulong)whosPlaying.Value].PlayerObject.gameObject 
        :  FindBotObject((ulong)whosPlaying.Value);

        // AI Cycles through all 3 phases automatically
        while (activePlayerObj != null && activePlayerObj.GetComponent<Character>().isRobot.Value)
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