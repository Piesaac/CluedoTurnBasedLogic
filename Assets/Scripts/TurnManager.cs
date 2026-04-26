using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using turnyWurny; // Ensure this matches your TurnStage enum

public class TurnManager : NetworkBehaviour
{
    [Header("UI Text")]
    public TextMeshProUGUI status;
    public TextMeshProUGUI activePlayerText;

    [Header("Settings")]
    public NetworkVariable<TurnStage> whatPhase = new NetworkVariable<TurnStage>(TurnStage.ROLLING);
    public NetworkVariable<int> whosPlaying = new NetworkVariable<int>(0);

    // List to keep track of the specific order: 0, 1, 100, 101...
    public List<ulong> turnOrder = new List<ulong>();
    private int allPlayers = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetupTurnOrder();
            // Start the game with the first ID in the list
            if (turnOrder.Count > 0) whosPlaying.Value = (int)turnOrder[0];
        }

        whatPhase.OnValueChanged += (oldVal, newVal) => updateUI();
        whosPlaying.OnValueChanged += (oldVal, newVal) => updateUI();
        updateUI();
    }

    private void SetupTurnOrder()
    {
        turnOrder.Clear();

        // 1. Add all human Client IDs
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            turnOrder.Add(clientId);
        }

        // 2. Add all Bot IDs based on the MenuController setting
        for (int i = 0; i < MenuController.numBotsToSpawn; i++)
        {
            turnOrder.Add((ulong)(100 + i));
        }

        allPlayers = turnOrder.Count;
        Debug.Log($"TurnManager: Sequence initialized with {allPlayers} players.");
    }

    public bool turingTest()
    {
        GameObject activeObj = GetActivePlayerObject();
        if (activeObj == null) return false;

        // Check the Character component for the robot identity tag
        if (activeObj.TryGetComponent<Character>(out var character))
        {
            return character.isRobot.Value;
        }

        return false;
    }

    private GameObject GetActivePlayerObject()
    {
        ulong activeId = (ulong)whosPlaying.Value;

        // Check for Human Players
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(activeId, out var client))
        {
            if (client.PlayerObject != null) return client.PlayerObject.gameObject;
        }

        // Check for Bot Players by searching for the botID variable
        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (netObj.TryGetComponent<Character>(out var character))
            {
                if (character.botID.Value == (int)activeId)
                {
                    return netObj.gameObject;
                }
            }
        }
        return null;
    }

    private void updateUI()
    {
        if (status != null) status.text = whatPhase.Value.ToString() + "!";

        if (activePlayerText != null)
        {
            string playerName = "";

            // Custom names for characters 0 and 1
            if (whosPlaying.Value == 0) playerName = "Miss Scarlett";
            else if (whosPlaying.Value == 1) playerName = "Colonel Mustard";
            else if (whosPlaying.Value >= 100) playerName = "Bot " + whosPlaying.Value;
            else playerName = "Player " + (whosPlaying.Value + 1);

            activePlayerText.text = playerName;

            if (whosPlaying.Value == (int)NetworkManager.Singleton.LocalClientId)
            {
                activePlayerText.text += " (YOU)";
            }
        }
    }

    public void reqNextPhase()
    {
        // Only allow progression if it's your turn or you are the Server
        if (NetworkManager.Singleton.LocalClientId == (ulong)whosPlaying.Value || IsServer)
        {
            nextPhaseServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void nextPhaseServerRpc()
    {
        if (whatPhase.Value == TurnStage.ROLLING) whatPhase.Value = TurnStage.MOVING;
        else if (whatPhase.Value == TurnStage.MOVING) whatPhase.Value = TurnStage.SUGGESTING;
        else nextTurn();
    }

    public void nextTurn()
    {
        if (!IsServer) return;

        // Find current ID in the turn list and move to the next index
        int currentIndex = turnOrder.IndexOf((ulong)whosPlaying.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;

        whosPlaying.Value = (int)turnOrder[nextIndex];
        whatPhase.Value = TurnStage.ROLLING;

        Debug.Log($"Turn passed to ID: {whosPlaying.Value}");
    }

    public void pushNextPhase()
    {
        if (!IsServer) return;
        nextPhaseServerRpc();
    }
}